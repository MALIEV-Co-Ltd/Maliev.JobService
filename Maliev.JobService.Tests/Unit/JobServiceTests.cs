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
using Microsoft.Extensions.Logging.Abstractions;
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

    #region UpdateDetailsAsync

    [Fact]
    public async Task UpdateDetailsAsync_WhenJobExists_UpdatesEditableProductionFields()
    {
        var materialId = Guid.NewGuid();
        var job = CreateTestJob(JobStatus.Queued);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateDetailsAsync(job.Id, new UpdateJobDetailsCommand
        {
            CustomerId = "  C-41901388  ",
            CustomerName = " AsianRider ",
            MaterialId = materialId,
            AssignedOperator = " Natthapol Vanasrivilai ",
            Priority = 1,
        }, "planner");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Job);
        Assert.Equal("C-41901388", result.Job.CustomerId);
        Assert.Equal("AsianRider", result.Job.CustomerName);
        Assert.Equal(materialId, result.Job.MaterialId);
        Assert.Equal("Natthapol Vanasrivilai", result.Job.AssignedOperator);
        Assert.Equal(1, result.Job.Priority);

        var persisted = await _dbContext.Jobs.FindAsync(job.Id);
        Assert.NotNull(persisted);
        Assert.Equal("C-41901388", persisted.CustomerId);
        Assert.Equal("AsianRider", persisted.CustomerName);
        Assert.Equal(materialId, persisted.MaterialId);
        Assert.Equal("Natthapol Vanasrivilai", persisted.AssignedOperator);
        Assert.Equal(1, persisted.Priority);
    }

    [Fact]
    public async Task UpdateDetailsAsync_WhenMaterialIdIsEmpty_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Queued);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.UpdateDetailsAsync(job.Id, new UpdateJobDetailsCommand
        {
            MaterialId = Guid.Empty,
        }, "planner");

        Assert.False(result.IsSuccess);
        Assert.Contains("MaterialId", result.Error, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task QueueAsync_WhenValid_AssignsSchedulingSlot()
    {
        var job = CreateTestJob(JobStatus.Pending);
        job.SetupTimeMinutes = 15;
        job.EstimatedPrintTimeMinutes = 120;
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var before = DateTime.UtcNow;
        await _service.QueueAsync(job.Id, "machine-slot", "user");

        var persisted = await _dbContext.Jobs.FindAsync(job.Id);
        Assert.NotNull(persisted!.ScheduledStartTime);
        Assert.NotNull(persisted.ScheduledEndTime);
        Assert.True(persisted.ScheduledStartTime >= before);
        Assert.Equal(1, persisted.QueuePosition);
        // End = Start + setup + print = Start + 135
        var expectedDuration = TimeSpan.FromMinutes(135);
        var actualDuration = persisted.ScheduledEndTime!.Value - persisted.ScheduledStartTime!.Value;
        Assert.Equal(expectedDuration, actualDuration);
    }

    [Fact]
    public async Task QueueAsync_TwoJobsSameMachine_SecondStartsAfterFirst()
    {
        var job1 = CreateTestJob(JobStatus.Pending);
        job1.SetupTimeMinutes = 15;
        job1.EstimatedPrintTimeMinutes = 120;
        var job2 = CreateTestJob(JobStatus.Pending);
        job2.SetupTimeMinutes = 15;
        job2.EstimatedPrintTimeMinutes = 60;
        _dbContext.Jobs.AddRange(job1, job2);
        await _dbContext.SaveChangesAsync();

        await _service.QueueAsync(job1.Id, "sequential-machine", "user");
        await _service.QueueAsync(job2.Id, "sequential-machine", "user");

        var p1 = await _dbContext.Jobs.FindAsync(job1.Id);
        var p2 = await _dbContext.Jobs.FindAsync(job2.Id);
        Assert.True(p2!.ScheduledStartTime >= p1!.ScheduledEndTime);
        Assert.Equal(2, p2.QueuePosition);
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

    [Fact]
    public async Task CompleteAsync_WhenValid_RoutesCompletionStatusChangeToDownstreamServices()
    {
        var job = CreateTestJob(JobStatus.Finishing);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        await _service.CompleteAsync(job.Id, "scanner-operator");

        _publishEndpointMock.Verify(
            p => p.Publish(
                It.Is<JobStatusChangedEvent>(evt =>
                    evt.Payload.JobId == job.Id &&
                    evt.Payload.PreviousStatus == JobStatus.Finishing.ToString() &&
                    evt.Payload.NewStatus == JobStatus.Completed.ToString() &&
                    evt.Payload.ChangedBy == "scanner-operator" &&
                    evt.ConsumedBy.Contains("OrderService") &&
                    evt.ConsumedBy.Contains("QualityService") &&
                    evt.ConsumedBy.Contains("NotificationService")),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        existingJob.OrderItemId = Guid.NewGuid();
        _dbContext.Jobs.Add(existingJob);
        await _dbContext.SaveChangesAsync();

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemDto>
            {
                new()
                {
                    OrderItemId = existingJob.OrderItemId,
                    MaterialId = existingJob.MaterialId,
                    Technology = existingJob.Technology,
                    VolumeCm3 = existingJob.VolumeCm3,
                    EstimatedPrintTimeMinutes = existingJob.EstimatedPrintTimeMinutes,
                },
            });

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WhenSomeItemJobsExist_CreatesMissingJobs()
    {
        var orderId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        var missingItemId = Guid.NewGuid();
        var existingJob = CreateTestJob();
        existingJob.OrderId = orderId;
        existingJob.OrderItemId = existingItemId;
        _dbContext.Jobs.Add(existingJob);
        await _dbContext.SaveChangesAsync();

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemDto>
            {
                new()
                {
                    OrderItemId = existingItemId,
                    MaterialId = existingJob.MaterialId,
                    Technology = existingJob.Technology,
                    VolumeCm3 = existingJob.VolumeCm3,
                    EstimatedPrintTimeMinutes = existingJob.EstimatedPrintTimeMinutes,
                },
                new()
                {
                    OrderItemId = missingItemId,
                    MaterialId = Guid.NewGuid(),
                    Technology = "SLA",
                    VolumeCm3 = 25,
                    EstimatedPrintTimeMinutes = 90,
                },
            });

        var result = await _service.CreateJobsForPaidOrderAsync(orderId);

        Assert.Equal(1, result);
        var jobs = await _dbContext.Jobs.Where(job => job.OrderId == orderId).ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, job => job.OrderItemId == existingItemId);
        Assert.Contains(jobs, job => job.OrderItemId == missingItemId);
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
    public async Task CreateJobsForPaidOrderAsync_WithOrderNumber_UsesOrderNumberLookup()
    {
        var orderId = Guid.NewGuid();
        var orderNumber = "ORD-2026-00123";
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
        };

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderItems);

        var result = await _service.CreateJobsForPaidOrderAsync(orderId, orderNumber);

        Assert.Equal(1, result);
        _orderServiceClientMock.Verify(
            c => c.GetOrderItemsAsync(orderNumber, It.IsAny<CancellationToken>()),
            Times.Once);
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
    public async Task CreateJobsForPaidOrderAsync_PopulatesSetupTimeMinutes()
    {
        var orderId = Guid.NewGuid();
        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemDto>
            {
                new() { OrderItemId = Guid.NewGuid(), MaterialId = Guid.NewGuid(),
                        Technology = "FDM", VolumeCm3 = 10, EstimatedPrintTimeMinutes = 0 },
                new() { OrderItemId = Guid.NewGuid(), MaterialId = Guid.NewGuid(),
                        Technology = "CNC", VolumeCm3 = 10, EstimatedPrintTimeMinutes = 0 },
            });

        await _service.CreateJobsForPaidOrderAsync(orderId);

        var jobs = await _dbContext.Jobs.Where(j => j.OrderId == orderId).ToListAsync();
        var fdm = jobs.First(j => j.Technology == "FDM");
        var cnc = jobs.First(j => j.Technology == "CNC");
        Assert.Equal(15, fdm.SetupTimeMinutes);  // FDM setup = 15 min
        Assert.Equal(60, cnc.SetupTimeMinutes);  // CNC setup = 60 min
        Assert.True(fdm.EstimatedPrintTimeMinutes >= 30);   // floor applied
        Assert.True(cnc.EstimatedPrintTimeMinutes >= 30);
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

    #region PlanningHolds

    [Fact]
    public async Task CreatePlanningHoldAsync_WithValidRequest_CreatesActiveHold()
    {
        var projectId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(2);

        var result = await _service.CreatePlanningHoldAsync(new CreatePlanningHoldCommand
        {
            ProjectId = projectId,
            ProjectPartId = partId,
            Technology = "FDM",
            MachineId = "FDM-01",
            MachineName = "FDM Printer 01",
            ScheduledStartTime = start,
            SetupTimeMinutes = 15,
            ProductionTimeMinutes = 45,
            Quantity = 3,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        }, "planner");

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanningHoldStatus.Active, result.Hold!.Status);
        Assert.Equal(projectId, result.Hold.ProjectId);
        Assert.Equal(partId, result.Hold.ProjectPartId);
        Assert.Equal("FDM-01", result.Hold.MachineId);
        Assert.Equal(1, result.Hold.QueuePosition);
        Assert.Equal(TimeSpan.FromMinutes(60), result.Hold.ScheduledEndTime - result.Hold.ScheduledStartTime);
    }

    [Fact]
    public async Task GetQueueDepthByTechnologyAsync_IncludesActivePlanningHolds()
    {
        await _service.CreatePlanningHoldAsync(new CreatePlanningHoldCommand
        {
            ProjectId = Guid.NewGuid(),
            ProjectPartId = Guid.NewGuid(),
            Technology = "FDM",
            MachineId = "FDM-02",
            ScheduledStartTime = DateTime.UtcNow.AddHours(1),
            SetupTimeMinutes = 15,
            ProductionTimeMinutes = 30,
            Quantity = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        }, "planner");

        var depth = await _service.GetQueueDepthByTechnologyAsync("FDM");

        Assert.True(depth.TryGetValue("FDM", out var count));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ExpirePlanningHoldsAsync_WhenExpired_MarksHoldExpired()
    {
        var hold = new ProductionPlanningHold
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ProjectPartId = Guid.NewGuid(),
            Technology = "CNC_MILL",
            MachineId = "CNC-01",
            QueuePosition = 1,
            ScheduledStartTime = DateTime.UtcNow.AddHours(1),
            ScheduledEndTime = DateTime.UtcNow.AddHours(2),
            SetupTimeMinutes = 60,
            ProductionTimeMinutes = 60,
            Quantity = 1,
            Status = PlanningHoldStatus.Active,
            CreatedBy = "planner",
            CreatedAt = DateTime.UtcNow.AddDays(-4),
            UpdatedAt = DateTime.UtcNow.AddDays(-4),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        _dbContext.ProductionPlanningHolds.Add(hold);
        await _dbContext.SaveChangesAsync();

        var expired = await _service.ExpirePlanningHoldsAsync(DateTime.UtcNow);

        var persisted = await _dbContext.ProductionPlanningHolds.FindAsync(hold.Id);
        Assert.Equal(1, expired);
        Assert.Equal(PlanningHoldStatus.Expired, persisted!.Status);
    }

    [Fact]
    public async Task CreateJobsForPaidOrderAsync_WithMatchingPlanningHold_ConvertsHoldToJobSchedule()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(4);
        var holdResult = await _service.CreatePlanningHoldAsync(new CreatePlanningHoldCommand
        {
            ProjectId = projectId,
            ProjectPartId = partId,
            Technology = "FDM",
            MachineId = "FDM-03",
            ScheduledStartTime = start,
            SetupTimeMinutes = 15,
            ProductionTimeMinutes = 60,
            Quantity = 2,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        }, "planner");

        _orderServiceClientMock
            .Setup(c => c.GetOrderItemsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemDto>
            {
                new()
                {
                    OrderItemId = Guid.NewGuid(),
                    SourceProjectId = projectId,
                    SourceProjectPartId = partId,
                    MaterialId = Guid.NewGuid(),
                    Technology = "FDM",
                    VolumeCm3 = 10,
                    Quantity = 2,
                    EstimatedPrintTimeMinutes = 30,
                }
            });

        await _service.CreateJobsForPaidOrderAsync(orderId);

        var job = await _dbContext.Jobs.SingleAsync(job => job.OrderId == orderId);
        var hold = await _dbContext.ProductionPlanningHolds.FindAsync(holdResult.Hold!.Id);
        Assert.Equal("FDM-03", job.AssignedMachineId);
        Assert.Equal(start, job.ScheduledStartTime);
        Assert.Equal(PlanningHoldStatus.Converted, hold!.Status);
        Assert.Equal(job.Id, hold.ConvertedJobId);
    }

    #endregion

    #region ReorderAsync

    [Fact]
    public async Task ReorderAsync_WhenJobNotFound_ReturnsNotFound()
    {
        var result = await _service.ReorderAsync(Guid.NewGuid(), 1);
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task ReorderAsync_WhenJobNotQueued_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Pending);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReorderAsync(job.Id, 1);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReorderAsync_WhenNoMachine_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = null;
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReorderAsync(job.Id, 1);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ReorderAsync_WhenValid_ReturnsSuccessWithUpdatedPosition()
    {
        var j1 = CreateTestJob(JobStatus.Queued); j1.AssignedMachineId = "reorder-m"; j1.QueuePosition = 1;
        var j2 = CreateTestJob(JobStatus.Queued); j2.AssignedMachineId = "reorder-m"; j2.QueuePosition = 2;
        _dbContext.Jobs.AddRange(j1, j2);
        await _dbContext.SaveChangesAsync();

        var result = await _service.ReorderAsync(j2.Id, 1, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var persisted = await _dbContext.Jobs.FindAsync(j2.Id);
        Assert.Equal(1, persisted!.QueuePosition);
    }

    #endregion

    #region GetMachineScheduleAsync

    [Fact]
    public async Task GetMachineScheduleAsync_ReturnsOnlyJobsInRange()
    {
        var inRange = CreateTestJob(JobStatus.Queued);
        inRange.AssignedMachineId = "sched-m";
        inRange.ScheduledStartTime = DateTime.UtcNow.AddDays(5);
        inRange.ScheduledEndTime = inRange.ScheduledStartTime!.Value.AddHours(2);

        var outOfRange = CreateTestJob(JobStatus.Queued);
        outOfRange.AssignedMachineId = "sched-m";
        outOfRange.ScheduledStartTime = DateTime.UtcNow.AddDays(60);
        outOfRange.ScheduledEndTime = outOfRange.ScheduledStartTime!.Value.AddHours(2);

        _dbContext.Jobs.AddRange(inRange, outOfRange);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetMachineScheduleAsync(
            "sched-m", DateTime.UtcNow, DateTime.UtcNow.AddDays(30));

        Assert.Single(result);
        Assert.Equal(inRange.Id, result[0].Id);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsJobsAndActiveHoldsAcrossRequestedMachines()
    {
        var projectId = Guid.NewGuid();
        var partId = Guid.NewGuid();
        var from = DateTime.UtcNow.Date;
        var jobStart = from.AddHours(8);
        var holdStart = from.AddHours(10);

        var fdmJob = CreateTestJob(JobStatus.Queued);
        fdmJob.AssignedMachineId = "FDM-01";
        fdmJob.SourceProjectId = projectId;
        fdmJob.SourceProjectPartId = partId;
        fdmJob.ScheduledStartTime = jobStart;
        fdmJob.ScheduledEndTime = jobStart.AddHours(2);
        fdmJob.QueuePosition = 1;

        _dbContext.Jobs.Add(fdmJob);
        await _service.CreatePlanningHoldAsync(new CreatePlanningHoldCommand
        {
            ProjectId = projectId,
            ProjectPartId = Guid.NewGuid(),
            Technology = "CNC_MILL",
            MachineId = "CNC-01",
            MachineName = "HAAS VF2",
            ScheduledStartTime = holdStart,
            ScheduledEndTime = holdStart.AddHours(1),
            SetupTimeMinutes = 60,
            ProductionTimeMinutes = 60,
            Quantity = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        }, "planner");
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetScheduleAsync(
            from,
            from.AddDays(1),
            ["FDM-01", "CNC-01"],
            ["FDM", "CNC_MILL"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, slot => slot.MachineId == "FDM-01" && slot.JobId == fdmJob.Id && !slot.IsHold);
        var holdSlot = Assert.Single(result, slot => slot.MachineId == "CNC-01" && slot.IsHold);
        Assert.Equal(projectId, holdSlot.ProjectId);
        Assert.Equal("HAAS VF2", holdSlot.MachineName);
    }

    [Fact]
    public async Task RescheduleAsync_WhenQueuedJobMovedToFreeSlot_UpdatesMachineAndTimes()
    {
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = "FDM-01";
        job.QueuePosition = 2;
        job.ScheduledStartTime = DateTime.UtcNow.AddHours(1);
        job.ScheduledEndTime = DateTime.UtcNow.AddHours(3);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var start = DateTime.UtcNow.AddHours(5);
        var end = start.AddHours(2);

        var result = await _service.RescheduleAsync(job.Id, new RescheduleJobCommand
        {
            MachineId = "CNC-01",
            ScheduledStartTime = start,
            ScheduledEndTime = end,
            QueuePosition = 1,
        });

        Assert.True(result.IsSuccess);
        var persisted = await _dbContext.Jobs.FindAsync(job.Id);
        Assert.Equal("CNC-01", persisted!.AssignedMachineId);
        Assert.Equal(start, persisted.ScheduledStartTime);
        Assert.Equal(end, persisted.ScheduledEndTime);
        Assert.Equal(1, persisted.QueuePosition);
    }

    [Fact]
    public async Task RescheduleAsync_WhenJobIsNotQueued_ReturnsFailure()
    {
        var job = CreateTestJob(JobStatus.InProgress);
        job.AssignedMachineId = "FDM-01";
        job.ScheduledStartTime = DateTime.UtcNow.AddHours(1);
        job.ScheduledEndTime = DateTime.UtcNow.AddHours(3);
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var result = await _service.RescheduleAsync(job.Id, new RescheduleJobCommand
        {
            MachineId = "FDM-01",
            ScheduledStartTime = DateTime.UtcNow.AddHours(4),
            ScheduledEndTime = DateTime.UtcNow.AddHours(5),
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("queued", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RescheduleAsync_WhenTargetSlotOverlapsActiveHold_ReturnsFailure()
    {
        var projectId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(4);
        var job = CreateTestJob(JobStatus.Queued);
        job.AssignedMachineId = "FDM-01";
        job.ScheduledStartTime = DateTime.UtcNow.AddHours(1);
        job.ScheduledEndTime = DateTime.UtcNow.AddHours(2);
        _dbContext.Jobs.Add(job);

        await _service.CreatePlanningHoldAsync(new CreatePlanningHoldCommand
        {
            ProjectId = projectId,
            ProjectPartId = Guid.NewGuid(),
            Technology = "FDM",
            MachineId = "FDM-02",
            ScheduledStartTime = start,
            ScheduledEndTime = start.AddHours(2),
            SetupTimeMinutes = 15,
            ProductionTimeMinutes = 105,
            Quantity = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(72),
        }, "planner");
        await _dbContext.SaveChangesAsync();

        var result = await _service.RescheduleAsync(job.Id, new RescheduleJobCommand
        {
            MachineId = "FDM-02",
            ScheduledStartTime = start.AddMinutes(30),
            ScheduledEndTime = start.AddHours(1),
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("overlap", result.Error, StringComparison.OrdinalIgnoreCase);
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



