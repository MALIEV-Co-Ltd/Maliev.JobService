using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Infrastructure.Persistence;
using Maliev.JobService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.JobService.Tests.Integration;

public class SchedulingServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
#pragma warning disable CS0618
        new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("scheddb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();
#pragma warning restore CS0618

    private JobDbContext _db = null!;
    private SchedulingService _sut = null!;
    private TimeEstimationService _estimation = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new JobDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _estimation = new TimeEstimationService();
        _sut = new SchedulingService(_db, NullLogger<SchedulingService>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private Job CreateJob(string machineId = "M1", JobStatus status = JobStatus.Queued,
        string technology = "FDM", decimal volumeCm3 = 10m)
    {
        var setupMins = _estimation.EstimateSetupTimeMinutes(technology);
        var printMins = _estimation.EstimatePrintTimeMinutes(technology, volumeCm3);
        return new Job
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OrderItemId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            Technology = technology,
            VolumeCm3 = volumeCm3,
            EstimatedPrintTimeMinutes = printMins,
            SetupTimeMinutes = setupMins,
            Priority = 5,
            Status = status,
            AssignedMachineId = machineId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    // ── ComputeSlotAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeSlotAsync_FirstJob_GetsPosition1AndFutureSlot()
    {
        var job = CreateJob();
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var before = DateTime.UtcNow;
        await _sut.ComputeSlotAsync(job, "M1");

        Assert.Equal(1, job.QueuePosition);
        Assert.NotNull(job.ScheduledStartTime);
        Assert.NotNull(job.ScheduledEndTime);
        Assert.True(job.ScheduledStartTime >= before);
        Assert.True(job.ScheduledEndTime > job.ScheduledStartTime);
    }

    [Fact]
    public async Task ComputeSlotAsync_SecondJob_StartsOneHourAfterFirstEnds()
    {
        var job1 = CreateJob();
        _db.Jobs.Add(job1);
        await _db.SaveChangesAsync();
        await _sut.ComputeSlotAsync(job1, "M1");
        job1.ScheduledStartTime = DateTime.UtcNow.AddHours(1);
        job1.ScheduledEndTime = DateTime.UtcNow.AddHours(3);
        await _db.SaveChangesAsync();

        var job2 = CreateJob();
        _db.Jobs.Add(job2);
        await _db.SaveChangesAsync();
        await _sut.ComputeSlotAsync(job2, "M1");

        Assert.Equal(2, job2.QueuePosition);
        Assert.True(job2.ScheduledStartTime >= job1.ScheduledEndTime!.Value.AddHours(1));
    }

    [Fact]
    public async Task ComputeSlotAsync_DifferentMachine_GetsPosition1()
    {
        var jobM1 = CreateJob("M1");
        _db.Jobs.Add(jobM1);
        await _db.SaveChangesAsync();
        await _sut.ComputeSlotAsync(jobM1, "M1");
        jobM1.ScheduledStartTime = DateTime.UtcNow.AddHours(1);
        jobM1.ScheduledEndTime = DateTime.UtcNow.AddHours(3);
        await _db.SaveChangesAsync();

        var jobM2 = CreateJob("M2");
        _db.Jobs.Add(jobM2);
        await _db.SaveChangesAsync();
        await _sut.ComputeSlotAsync(jobM2, "M2");

        Assert.Equal(1, jobM2.QueuePosition);
    }

    // ── RescheduleQueueAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RescheduleQueueAsync_AfterCancellation_CompactsPositions()
    {
        // Job 1, 2, 3 queued → cancel job 2 → reschedule → positions should be 1, 2
        var jobs = new[] { CreateJob(), CreateJob(), CreateJob() };
        _db.Jobs.AddRange(jobs);
        await _db.SaveChangesAsync();

        // Assign positions manually
        jobs[0].QueuePosition = 1; jobs[0].ScheduledStartTime = DateTime.UtcNow.AddMinutes(10); jobs[0].ScheduledEndTime = DateTime.UtcNow.AddMinutes(130);
        jobs[1].QueuePosition = 2; jobs[1].ScheduledStartTime = DateTime.UtcNow.AddMinutes(130); jobs[1].ScheduledEndTime = DateTime.UtcNow.AddMinutes(250);
        jobs[2].QueuePosition = 3; jobs[2].ScheduledStartTime = DateTime.UtcNow.AddMinutes(250); jobs[2].ScheduledEndTime = DateTime.UtcNow.AddMinutes(370);
        await _db.SaveChangesAsync();

        // Cancel job 2
        jobs[1].Status = JobStatus.Cancelled;
        jobs[1].AssignedMachineId = null;
        await _db.SaveChangesAsync();

        await _sut.RescheduleQueueAsync("M1");

        var refreshed = await _db.Jobs.Where(j => j.Status == JobStatus.Queued && j.AssignedMachineId == "M1").OrderBy(j => j.QueuePosition).ToListAsync();
        Assert.Equal(2, refreshed.Count);
        Assert.Equal(1, refreshed[0].QueuePosition);
        Assert.Equal(2, refreshed[1].QueuePosition);
        Assert.True(refreshed[1].ScheduledStartTime >= refreshed[0].ScheduledEndTime!.Value.AddHours(1));
    }

    [Fact]
    public async Task RescheduleQueueAsync_EmptyMachine_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.RescheduleQueueAsync("EMPTY_MACHINE"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task RescheduleQueueAsync_WithInProgressAnchor_CascadesFromItsEnd()
    {
        var inProgress = CreateJob(status: JobStatus.InProgress);
        inProgress.QueuePosition = 0;
        inProgress.ScheduledStartTime = DateTime.UtcNow;
        inProgress.ScheduledEndTime = DateTime.UtcNow.AddHours(2);

        var queued = CreateJob();
        queued.QueuePosition = 5; // will be reassigned

        _db.Jobs.AddRange(inProgress, queued);
        await _db.SaveChangesAsync();

        await _sut.RescheduleQueueAsync("M1");

        await _db.Entry(queued).ReloadAsync();
        await _db.Entry(inProgress).ReloadAsync();

        Assert.Equal(1, queued.QueuePosition);
        Assert.NotNull(queued.ScheduledStartTime);
        var expectedStart = inProgress.ScheduledEndTime!.Value.AddHours(1);
        var diffMs = Math.Abs((queued.ScheduledStartTime!.Value - expectedStart).TotalMilliseconds);
        Assert.True(diffMs < 1000, $"ScheduledStart ({queued.ScheduledStartTime}) should be within 1s of quiet-gap start ({expectedStart}), diff={diffMs}ms");
    }

    // ── ReorderJobAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderJobAsync_MovesJobToFront()
    {
        var j1 = CreateJob(); j1.QueuePosition = 1; j1.ScheduledStartTime = DateTime.UtcNow.AddMinutes(10); j1.ScheduledEndTime = DateTime.UtcNow.AddMinutes(130);
        var j2 = CreateJob(); j2.QueuePosition = 2; j2.ScheduledStartTime = DateTime.UtcNow.AddMinutes(130); j2.ScheduledEndTime = DateTime.UtcNow.AddMinutes(250);
        var j3 = CreateJob(); j3.QueuePosition = 3; j3.ScheduledStartTime = DateTime.UtcNow.AddMinutes(250); j3.ScheduledEndTime = DateTime.UtcNow.AddMinutes(370);
        _db.Jobs.AddRange(j1, j2, j3);
        await _db.SaveChangesAsync();

        await _sut.ReorderJobAsync(j3.Id, 1, "M1");

        await _db.Entry(j3).ReloadAsync();
        Assert.Equal(1, j3.QueuePosition);

        await _db.Entry(j1).ReloadAsync();
        await _db.Entry(j2).ReloadAsync();
        Assert.True(j1.QueuePosition > j3.QueuePosition);
        Assert.True(j2.QueuePosition > j1.QueuePosition);
    }

    [Fact]
    public async Task ReorderJobAsync_NonExistentJob_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.ReorderJobAsync(Guid.NewGuid(), 1, "M1"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ReorderJobAsync_PositionClampedToListBounds()
    {
        var j1 = CreateJob(); j1.QueuePosition = 1;
        var j2 = CreateJob(); j2.QueuePosition = 2;
        _db.Jobs.AddRange(j1, j2);
        await _db.SaveChangesAsync();

        // Position 999 → clamped to last
        await _sut.ReorderJobAsync(j1.Id, 999, "M1");

        await _db.Entry(j1).ReloadAsync();
        await _db.Entry(j2).ReloadAsync();
        Assert.True(j1.QueuePosition > j2.QueuePosition);
    }

    // ── GetMachineScheduleAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetMachineScheduleAsync_ReturnsJobsInRange()
    {
        var inRange = CreateJob();
        inRange.ScheduledStartTime = DateTime.UtcNow.AddDays(5);
        inRange.ScheduledEndTime = inRange.ScheduledStartTime!.Value.AddHours(2);

        var outOfRange = CreateJob();
        outOfRange.ScheduledStartTime = DateTime.UtcNow.AddDays(40);
        outOfRange.ScheduledEndTime = outOfRange.ScheduledStartTime!.Value.AddHours(2);

        _db.Jobs.AddRange(inRange, outOfRange);
        await _db.SaveChangesAsync();

        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(30);
        var result = await _sut.GetMachineScheduleAsync("M1", from, to);

        Assert.Single(result);
        Assert.Equal(inRange.Id, result[0].Id);
    }

    [Fact]
    public async Task GetMachineScheduleAsync_FiltersToCorrectMachine()
    {
        var m1Job = CreateJob("M1");
        m1Job.ScheduledStartTime = DateTime.UtcNow.AddDays(1);
        m1Job.ScheduledEndTime = m1Job.ScheduledStartTime!.Value.AddHours(2);

        var m2Job = CreateJob("M2");
        m2Job.ScheduledStartTime = DateTime.UtcNow.AddDays(1);
        m2Job.ScheduledEndTime = m2Job.ScheduledStartTime!.Value.AddHours(2);

        _db.Jobs.AddRange(m1Job, m2Job);
        await _db.SaveChangesAsync();

        var result = await _sut.GetMachineScheduleAsync("M1", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        Assert.Single(result);
        Assert.Equal(m1Job.Id, result[0].Id);
    }

    [Fact]
    public async Task GetMachineScheduleAsync_OrderedByScheduledStartTime()
    {
        var late = CreateJob();
        late.ScheduledStartTime = DateTime.UtcNow.AddDays(10);
        late.ScheduledEndTime = late.ScheduledStartTime!.Value.AddHours(2);

        var early = CreateJob();
        early.ScheduledStartTime = DateTime.UtcNow.AddDays(2);
        early.ScheduledEndTime = early.ScheduledStartTime!.Value.AddHours(2);

        _db.Jobs.AddRange(late, early);
        await _db.SaveChangesAsync();

        var result = await _sut.GetMachineScheduleAsync("M1", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        Assert.Equal(2, result.Count);
        Assert.Equal(early.Id, result[0].Id);
        Assert.Equal(late.Id, result[1].Id);
    }
}
