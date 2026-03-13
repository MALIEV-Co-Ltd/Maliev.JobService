using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Application.Models;
using Maliev.JobService.Domain.Clients;
using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Domain.Models;
using Maliev.JobService.Infrastructure.Metrics;
using Maliev.JobService.Infrastructure.Persistence;
using Maliev.JobService.Infrastructure.Services;
using Maliev.MessagingContracts.Contracts.Jobs;
using MassTransit;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.JobService.Tests.Unit;

public class JobServiceTests : IAsyncLifetime
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

    public JobServiceTests()
    {
    }

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

        _service = new Infrastructure.Services.JobService(
            _dbContext,
            _publishEndpointMock.Object,
            _orderServiceClientMock.Object,
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

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_WhenJobExists_ReturnsJob()
    {
        var job = CreateTestJob();
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetByIdAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenJobDoesNotExist_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region GetJobsAsync

    [Fact]
    public async Task GetJobsAsync_ReturnsAllJobs_WhenNoFilters()
    {
        var jobs = new[] { CreateTestJob(), CreateTestJob() };
        _dbContext.Jobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, null, null, 1, 10);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByStatus()
    {
        var pendingJob = CreateTestJob(JobStatus.Pending);
        var queuedJob = CreateTestJob(JobStatus.Queued);
        _dbContext.Jobs.AddRange(pendingJob, queuedJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(JobStatus.Pending, null, null, 1, 10);

        Assert.Equal(1, result.Total);
        Assert.Equal(JobStatus.Pending, result.Items[0].Status);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByTechnology()
    {
        var fdmJob = CreateTestJob();
        fdmJob.Technology = "FDM";
        var slaJob = CreateTestJob();
        slaJob.Technology = "SLA";
        _dbContext.Jobs.AddRange(fdmJob, slaJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, "FDM", null, 1, 10);

        Assert.Equal(1, result.Total);
        Assert.Equal("FDM", result.Items[0].Technology);
    }

    [Fact]
    public async Task GetJobsAsync_FiltersByMachineId()
    {
        var job1 = CreateTestJob(JobStatus.Queued);
        job1.AssignedMachineId = "machine-1";
        var job2 = CreateTestJob(JobStatus.Queued);
        job2.AssignedMachineId = "machine-2";
        _dbContext.Jobs.AddRange(job1, job2);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, null, "machine-1", 1, 10);

        Assert.Equal(1, result.Total);
        Assert.Equal("machine-1", result.Items[0].AssignedMachineId);
    }

    [Fact]
    public async Task GetJobsAsync_AppliesPagination()
    {
        var jobs = Enumerable.Range(0, 20).Select(_ => CreateTestJob()).ToList();
        _dbContext.Jobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, null, null, 2, 5);

        Assert.Equal(20, result.Total);
        Assert.Equal(4, result.TotalPages);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task GetJobsAsync_DefaultsInvalidPageValues()
    {
        var jobs = Enumerable.Range(0, 5).Select(_ => CreateTestJob()).ToList();
        _dbContext.Jobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, null, null, -1, 0);

        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetJobsAsync_ClampsPageSizeTo100()
    {
        var jobs = Enumerable.Range(0, 5).Select(_ => CreateTestJob()).ToList();
        _dbContext.Jobs.AddRange(jobs);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetJobsAsync(null, null, null, 1, 500);

        Assert.Equal(100, result.PageSize);
    }

    #endregion

    #region GetKanbanJobsAsync

    [Fact]
    public async Task GetKanbanJobsAsync_ReturnsActiveJobs()
    {
        var pendingJob = CreateTestJob(JobStatus.Pending);
        var inProgressJob = CreateTestJob(JobStatus.InProgress);
        var completedJob = CreateTestJob(JobStatus.Completed);
        completedJob.UpdatedAt = DateTime.UtcNow.AddDays(-10);
        _dbContext.Jobs.AddRange(pendingJob, inProgressJob, completedJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetKanbanJobsAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetKanbanJobsAsync_OrdersByPriority()
    {
        var lowPriority = CreateTestJob(JobStatus.Pending);
        lowPriority.Priority = 10;
        var highPriority = CreateTestJob(JobStatus.Pending);
        highPriority.Priority = 1;
        _dbContext.Jobs.AddRange(lowPriority, highPriority);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetKanbanJobsAsync();

        Assert.Equal(1, result[0].Priority);
    }

    #endregion

    #region QueueAsync

    [Fact]
    public async Task QueueAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.QueueAsync(Guid.NewGuid(), "machine-1", "user");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task QueueAsync_WhenInvalidTransition_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.InProgress);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.QueueAsync(job.Id, "machine-1", "user");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task QueueAsync_WhenValid_TransitionsToQueued()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.QueueAsync(job.Id, "machine-1", "user");

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Queued, result.Job!.Status);
        Assert.Equal("machine-1", result.Job.AssignedMachineId);
    }

    #endregion

    #region StartAsync

    [Fact]
    public async Task StartAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.StartAsync(Guid.NewGuid(), "user");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task StartAsync_WhenInvalidTransition_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Completed);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.StartAsync(job.Id, "user");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task StartAsync_WhenValid_SetsStartedAt()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.StartAsync(job.Id, "user");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Job!.StartedAt);
        Assert.Equal(JobStatus.InProgress, result.Job.Status);
    }

    [Fact]
    public async Task StartAsync_WhenValid_PublishesJobStartedEvent()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        await _service.StartAsync(job.Id, "user");

        _publishEndpointMock.Verify(
            p => p.Publish(It.IsAny<JobStartedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FinishAsync

    [Fact]
    public async Task FinishAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.FinishAsync(Guid.NewGuid(), "user");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task FinishAsync_WhenInvalidTransition_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.FinishAsync(job.Id, "user");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task FinishAsync_WhenValid_TransitionsToFinishing()
    {
        var job = CreateTestJob(JobStatus.InProgress);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.FinishAsync(job.Id, "user");

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Finishing, result.Job!.Status);
    }

    #endregion

    #region CompleteAsync

    [Fact]
    public async Task CompleteAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.CompleteAsync(Guid.NewGuid(), "user");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task CompleteAsync_WhenInvalidTransition_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CompleteAsync(job.Id, "user");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CompleteAsync_WhenValid_SetsCompletedAt()
    {
        var job = CreateTestJob(JobStatus.Finishing);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CompleteAsync(job.Id, "user");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Job!.CompletedAt);
        Assert.Equal(JobStatus.Completed, result.Job.Status);
    }

    #endregion

    #region CancelAsync

    [Fact]
    public async Task CancelAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.CancelAsync(Guid.NewGuid(), "reason", "user");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task CancelAsync_WhenValid_SetsNotes()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CancelAsync(job.Id, "Customer request", "user");

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Cancelled, result.Job!.Status);
        Assert.Equal("Customer request", result.Job.Notes);
    }

    [Fact]
    public async Task CancelAsync_FromCompleted_IsValid()
    {
        var job = CreateTestJob(JobStatus.Completed);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CancelAsync(job.Id, "reason", "user");

        Assert.True(result.IsSuccess);
    }

    #endregion

    #region ReassignAsync

    [Fact]
    public async Task ReassignAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.ReassignAsync(Guid.NewGuid(), "machine-2");

        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task ReassignAsync_WhenNotQueued_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReassignAsync(job.Id, "machine-2");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReassignAsync_WhenValid_ChangesMachineId()
    {
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = "machine-1";
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReassignAsync(job.Id, "machine-2");

        Assert.True(result.IsSuccess);
        Assert.Equal("machine-2", result.Job!.AssignedMachineId);
    }

    #endregion

    #region CreateJobsForPaidOrderAsync

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WhenJobsExist_ReturnsZero()
    {
        var orderId = Guid.NewGuid();
        var existingJob = CreateTestJob();
        existingJob.OrderId = orderId;
        _dbContext.Jobs.Add(existingJob);
        await _dbContext.SaveChangesAsync();

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WhenNoOrderItems_ReturnsZero()
    {
        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemDto>());

        var result = await _service.CreateJobsForPaidOrderAsync(Guid.NewGuid());

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WhenValid_CreatesJobs()
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
                DeliveryDate = DateTime.UtcNow.AddDays(7),
            },
            new()
            {
                OrderItemId = Guid.NewGuid(),
                MaterialId = Guid.NewGuid(),
                Technology = "SLA",
                VolumeCm3 = 50,
                EstimatedPrintTimeMinutes = 60,
                DeliveryDate = null,
            },
        };

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItems);

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        Assert.Equal(2, result);
        var jobs = await _dbContext.Jobs.ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(JobStatus.Pending, j.Status));
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_CalculatesPriority()
    {
        var orderId = Guid.NewGuid();
        var deliveryDate = DateTime.UtcNow.AddDays(10).AddHours(12);
        var orderItems = new List<OrderItemDto>
        {
            new()
            {
                OrderItemId = Guid.NewGuid(),
                MaterialId = Guid.NewGuid(),
                Technology = "FDM",
                VolumeCm3 = 100,
                EstimatedPrintTimeMinutes = 120,
                DeliveryDate = deliveryDate,
            },
        };

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItems);

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        var job = await _dbContext.Jobs.FirstAsync();
        Assert.Equal(10, job.Priority);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WithNoDeliveryDate_SetsPriority999()
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
                DeliveryDate = null,
            },
        };

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItems);

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        var job = await _dbContext.Jobs.FirstAsync();
        Assert.Equal(999, job.Priority);
    }

    #endregion
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



