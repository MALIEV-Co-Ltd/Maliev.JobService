using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Domain.Clients;
using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Domain.Models;
using Maliev.JobService.Infrastructure.Metrics;
using Maliev.JobService.Infrastructure.Persistence;
using Maliev.JobService.Infrastructure.Services;
using MassTransit;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.JobService.Tests.Integration;

public class JobServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
#pragma warning disable CS0618
        new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("jobdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();
#pragma warning restore CS0618

    private JobDbContext _dbContext = null!;
    private Mock<IPublishEndpoint> _publishEndpointMock = null!;
    private Mock<IOrderServiceClient> _orderServiceClientMock = null!;
    private Mock<ILogger<Infrastructure.Services.JobService>> _loggerMock = null!;
    private JobMetrics _metrics = null!;
    private Infrastructure.Services.JobService _service = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new JobDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _orderServiceClientMock = new Mock<IOrderServiceClient>();
        _loggerMock = new Mock<ILogger<Infrastructure.Services.JobService>>();

        var meterFactory = new TestMeterFactory();
        _metrics = new JobMetrics(meterFactory);

        var scheduling = new SchedulingService(_dbContext, NullLogger<SchedulingService>.Instance);
        var estimation = new TimeEstimationService();

        _service = new Infrastructure.Services.JobService(
            _dbContext,
            _publishEndpointMock.Object,
            _orderServiceClientMock.Object,
            scheduling,
            estimation,
            _metrics,
            _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
        await _postgres.DisposeAsync();
    }

    private Job CreateTestJob(JobStatus status = JobStatus.Pending, Guid? id = null)
    {
        return new Job
        {
            Id = id ?? Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            OrderItemId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            Technology = "FDM",
            VolumeCm3 = 100,
            EstimatedPrintTimeMinutes = 120,
            Priority = 5,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task GetByIdAsync_WithRealDb_ReturnsJob()
    {
        var job = CreateTestJob();
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.GetByIdAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
    }

    [Fact]
    public async Task QueueAsync_WithRealDb_PersistsChanges()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.QueueAsync(job.Id, "machine-1", "testuser");

        Assert.True(result.IsSuccess);

        var updatedJob = await _dbContext.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Queued, updatedJob.Status);
        Assert.Equal("machine-1", updatedJob.AssignedMachineId);
        // Scheduling slot must be assigned
        Assert.NotNull(updatedJob.ScheduledStartTime);
        Assert.NotNull(updatedJob.ScheduledEndTime);
        Assert.Equal(1, updatedJob.QueuePosition);
    }

    [Fact]
    public async Task QueueAsync_WithRealDb_PersistsStatusTransitionAudit()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var result = await _service!.QueueAsync(job.Id, "machine-1", "scanner-user");
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);
        var audit = await _dbContext.JobStatusTransitionAudits.SingleAsync(audit => audit.JobId == job.Id);
        Assert.Equal(JobStatus.Pending, audit.PreviousStatus);
        Assert.Equal(JobStatus.Queued, audit.NewStatus);
        Assert.Equal("scanner-user", audit.ChangedBy);
        Assert.True(audit.ChangedAtUtc >= before);
        Assert.True(audit.ChangedAtUtc <= after);
    }

    [Fact]
    public async Task GetStatusTransitionAuditsAsync_WithRealDb_ReturnsOrderedAuditTrail()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        Assert.True((await _service!.QueueAsync(job.Id, "machine-1", "queue-user")).IsSuccess);
        Assert.True((await _service.StartAsync(job.Id, "start-user")).IsSuccess);

        var audits = await _service.GetStatusTransitionAuditsAsync(job.Id);

        Assert.Equal(2, audits.Count);
        Assert.Equal(JobStatus.Pending, audits[0].PreviousStatus);
        Assert.Equal(JobStatus.Queued, audits[0].NewStatus);
        Assert.Equal("queue-user", audits[0].ChangedBy);
        Assert.Equal(JobStatus.Queued, audits[1].PreviousStatus);
        Assert.Equal(JobStatus.InProgress, audits[1].NewStatus);
        Assert.Equal("start-user", audits[1].ChangedBy);
        Assert.True(audits[0].ChangedAtUtc <= audits[1].ChangedAtUtc);
    }

    [Fact]
    public async Task RescheduleAsync_StartsWithinOneHourAfterExistingJob_ReturnsFailure()
    {
        var existing = CreateTestJob(JobStatus.Queued);
        existing.AssignedMachineId = "machine-1";
        existing.QueuePosition = 1;
        existing.ScheduledStartTime = DateTime.UtcNow.AddHours(2);
        existing.ScheduledEndTime = DateTime.UtcNow.AddHours(4);

        var target = CreateTestJob(JobStatus.Queued);
        target.AssignedMachineId = "machine-1";
        target.QueuePosition = 2;
        target.ScheduledStartTime = DateTime.UtcNow.AddHours(6);
        target.ScheduledEndTime = DateTime.UtcNow.AddHours(8);

        _dbContext!.Jobs.AddRange(existing, target);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.RescheduleAsync(target.Id, new()
        {
            MachineId = "machine-1",
            ScheduledStartTime = existing.ScheduledEndTime.Value.AddMinutes(30),
            ScheduledEndTime = existing.ScheduledEndTime.Value.AddHours(2)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("quiet", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAsync_WithRealDb_SetsStartedAtTimestamp()
    {
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = "machine-1";
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.StartAsync(job.Id, "testuser");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Job!.StartedAt);

        var updatedJob = await _dbContext.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.NotNull(updatedJob.StartedAt);
    }

    [Fact]
    public async Task CompleteAsync_WithRealDb_SetsCompletedAtTimestamp()
    {
        var job = CreateTestJob(JobStatus.Finishing);
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.CompleteAsync(job.Id, "testuser");

        Assert.True(result.IsSuccess);

        var updatedJob = await _dbContext.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.NotNull(updatedJob.CompletedAt);
        Assert.Equal(JobStatus.Completed, updatedJob.Status);
    }

    [Fact]
    public async Task GetJobsAsync_WithRealDb_AppliesFilters()
    {
        var job1 = CreateTestJob(JobStatus.Pending);
        job1.Technology = "FDM";

        var job2 = CreateTestJob(JobStatus.Pending);
        job2.Technology = "SLA";

        var job3 = CreateTestJob(JobStatus.Queued);
        job3.Technology = "FDM";

        _dbContext!.Jobs.AddRange(job1, job2, job3);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.GetJobsAsync(JobStatus.Pending, "FDM", null, 1, 10);

        Assert.Equal(1, result.Total);
        Assert.Equal("FDM", result.Items[0].Technology);
        Assert.Equal(JobStatus.Pending, result.Items[0].Status);
    }

    [Fact]
    public async Task GetKanbanJobsAsync_WithRealDb_ReturnsRecentJobs()
    {
        var activeJob = CreateTestJob(JobStatus.InProgress);
        activeJob.UpdatedAt = DateTime.UtcNow;

        var oldCompletedJob = CreateTestJob(JobStatus.Completed);
        oldCompletedJob.UpdatedAt = DateTime.UtcNow.AddDays(-10);

        _dbContext!.Jobs.AddRange(activeJob, oldCompletedJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.GetKanbanJobsAsync();

        Assert.Single(result);
        Assert.Equal(JobStatus.InProgress, result[0].Status);
    }

    [Fact]
    public async Task CancelAsync_WithRealDb_SavesCancellationReason()
    {
        var job = CreateTestJob(JobStatus.InProgress);
        job.Notes = "Original note";
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.CancelAsync(job.Id, "Material unavailable", "testuser");

        Assert.True(result.IsSuccess);

        var updatedJob = await _dbContext.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Cancelled, updatedJob.Status);
        Assert.Equal("Material unavailable", updatedJob.Notes);
    }

    [Fact]
    public async Task ReassignAsync_WithRealDb_UpdatesMachineId()
    {
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = "machine-1";
        _dbContext!.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.ReassignAsync(job.Id, "machine-2");

        Assert.True(result.IsSuccess);

        var updatedJob = await _dbContext.Jobs.FirstAsync(j => j.Id == job.Id);
        Assert.Equal("machine-2", updatedJob.AssignedMachineId);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WithRealDb_CreatesJobsWithCorrectPriority()
    {
        var orderId = Guid.NewGuid();
        var orderItems = new List<OrderItemDto>
        {
            new()
            {
                OrderItemId = Guid.NewGuid(),
                MaterialId = Guid.NewGuid(),
                Technology = "FDM",
                VolumeCm3 = 100,
                EstimatedPrintTimeMinutes = 120,
                DeliveryDate = DateTime.UtcNow.AddDays(10).AddHours(12),
            },
        };

        _orderServiceClientMock!
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItems);

        var result = await _service!.CreateJobsForPaidOrderAsync(orderId);

        Assert.Equal(1, result);

        var createdJob = await _dbContext!.Jobs.FirstAsync(j => j.OrderId == orderId);
        Assert.Equal(10, createdJob.Priority);
        Assert.Equal(JobStatus.Pending, createdJob.Status);
    }

    [Fact]
    public async Task GetJobsAsync_OrdersByCreatedAtDescending()
    {
        var oldJob = CreateTestJob();
        oldJob.CreatedAt = DateTime.UtcNow.AddDays(-2);

        var newJob = CreateTestJob();
        newJob.CreatedAt = DateTime.UtcNow;

        _dbContext!.Jobs.AddRange(oldJob, newJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service!.GetJobsAsync(null, null, null, 1, 10);

        Assert.Equal(newJob.Id, result.Items[0].Id);
    }
}

internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly Dictionary<string, Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        if (!_meters.TryGetValue(options.Name, out var meter))
        {
            meter = new Meter(options.Name);
            _meters[options.Name] = meter;
        }
        return meter;
    }

    public void Dispose()
    {
    }
}



