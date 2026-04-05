using Maliev.JobService.Application.Abstractions;
using Maliev.JobService.Application.Models;
using Maliev.JobService.Domain.Clients;
using Maliev.JobService.Domain.Entities;
using Maliev.JobService.Infrastructure.Metrics;
using Maliev.JobService.Infrastructure.Persistence;
using Maliev.MessagingContracts;
using Maliev.MessagingContracts.Contracts.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.JobService.Infrastructure.Services;

/// <summary>
/// Constants for the JobService implementation.
/// </summary>
public static class JobServiceConstants
{
    /// <summary>
    /// Maximum page size for paginated queries.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Number of days to look back for recent jobs in Kanban view.
    /// </summary>
    public const int RecentDaysThreshold = 7;

    /// <summary>
    /// Default priority value when no delivery date is specified.
    /// </summary>
    public const int DefaultPriorityForNoDelivery = 999;
}

/// <summary>
/// Default implementation of <see cref="IJobService"/>.
/// </summary>
public class JobService : IJobService
{
    private readonly JobDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly ISchedulingService _schedulingService;
    private readonly ITimeEstimationService _timeEstimation;
    private readonly JobMetrics _metrics;
    private readonly ILogger<JobService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobService"/> class.
    /// </summary>
    public JobService(
        JobDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IOrderServiceClient orderServiceClient,
        ISchedulingService schedulingService,
        ITimeEstimationService timeEstimation,
        JobMetrics metrics,
        ILogger<JobService> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _orderServiceClient = orderServiceClient;
        _schedulingService = schedulingService;
        _timeEstimation = timeEstimation;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<Job>> GetJobsAsync(
        JobStatus? status,
        string? technology,
        string? assignedMachineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, JobServiceConstants.MaxPageSize);

        var query = _dbContext.Jobs.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            query = query.Where(j => j.Technology == technology);
        }

        if (!string.IsNullOrWhiteSpace(assignedMachineId))
        {
            query = query.Where(j => j.AssignedMachineId == assignedMachineId);
        }

        var total = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)safePageSize);

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Job>
        {
            Items = items,
            Total = total,
            Page = safePage,
            PageSize = safePageSize,
            TotalPages = totalPages,
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Job>> GetKanbanJobsAsync(CancellationToken cancellationToken = default)
    {
        var recentThreshold = DateTime.UtcNow.AddDays(-JobServiceConstants.RecentDaysThreshold);

        return await _dbContext.Jobs
            .AsNoTracking()
            .Where(j => (j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled) || j.UpdatedAt > recentThreshold)
            .OrderBy(j => j.Priority)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> QueueAsync(
        Guid id,
        string machineId,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        var (valid, error) = ValidateTransition(job.Status, "queue");
        if (!valid)
        {
            return JobOperationResult.Failure(error);
        }

        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;

        job.Status = JobStatus.Queued;
        job.AssignedMachineId = machineId;
        job.UpdatedAt = DateTime.UtcNow;

        await _schedulingService.ComputeSlotAsync(job, machineId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishJobStatusChangedAsync(job, previousStatus, changedBy, cancellationToken);

        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} queued on machine {MachineId}", job.Id, machineId);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> StartAsync(Guid id, string changedBy, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        var (valid, error) = ValidateTransition(job.Status, "start");
        if (!valid)
        {
            return JobOperationResult.Failure(error);
        }

        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;

        job.Status = JobStatus.InProgress;
        job.StartedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _publishEndpoint.Publish(new JobStartedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(JobStartedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "job-service",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobStartedEventPayload
            {
                JobId = job.Id,
                OrderId = job.OrderId,
                MaterialId = job.MaterialId,
                VolumeCm3 = (double)job.VolumeCm3,
                Technology = job.Technology,
                AssignedMachineId = job.AssignedMachineId ?? string.Empty,
                StartedAt = job.StartedAt.Value,
            }), cancellationToken);

        await PublishJobStatusChangedAsync(job, previousStatus, changedBy, cancellationToken);

        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} started at {StartedAt}", job.Id, job.StartedAt);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> FinishAsync(Guid id, string changedBy, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        var (valid, error) = ValidateTransition(job.Status, "finish");
        if (!valid)
        {
            return JobOperationResult.Failure(error);
        }

        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;

        job.Status = JobStatus.Finishing;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishJobStatusChangedAsync(job, previousStatus, changedBy, cancellationToken);

        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} moved to finishing", job.Id);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> CompleteAsync(Guid id, string changedBy, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        var (valid, error) = ValidateTransition(job.Status, "complete");
        if (!valid)
        {
            return JobOperationResult.Failure(error);
        }

        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;

        var completedMachineId = job.AssignedMachineId;
        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishJobStatusChangedAsync(job, previousStatus, changedBy, cancellationToken);

        if (!string.IsNullOrEmpty(completedMachineId))
            await _schedulingService.RescheduleQueueAsync(completedMachineId, cancellationToken);

        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} completed at {CompletedAt}", job.Id, job.CompletedAt);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> CancelAsync(
        Guid id,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        var previousStatus = job.Status;
        var transitionStart = job.UpdatedAt;
        var cancelledMachineId = job.AssignedMachineId;

        job.Status = JobStatus.Cancelled;
        job.Notes = reason;
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await PublishJobStatusChangedAsync(job, previousStatus, changedBy, cancellationToken);

        if (!string.IsNullOrEmpty(cancelledMachineId))
            await _schedulingService.RescheduleQueueAsync(cancelledMachineId, cancellationToken);

        _metrics.RecordTransition(previousStatus.ToString(), job.Status.ToString(), DateTime.UtcNow - transitionStart);
        _logger.LogInformation("Job {JobId} cancelled with reason: {Reason}", job.Id, reason);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> ReassignAsync(Guid id, string machineId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        if (job.Status != JobStatus.Queued)
        {
            return JobOperationResult.Failure("Can only reassign jobs in Queued status");
        }

        var oldMachineId = job.AssignedMachineId;
        job.AssignedMachineId = machineId;
        job.UpdatedAt = DateTime.UtcNow;

        await _schedulingService.ComputeSlotAsync(job, machineId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(oldMachineId) && oldMachineId != machineId)
            await _schedulingService.RescheduleQueueAsync(oldMachineId, cancellationToken);

        _logger.LogInformation("Job {JobId} reassigned to machine {MachineId}", job.Id, machineId);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<int> CreateJobsForPaidOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var existingJobs = await _dbContext.Jobs.AnyAsync(j => j.OrderId == orderId, cancellationToken);
        if (existingJobs)
        {
            _logger.LogInformation("Jobs already exist for OrderId: {OrderId}, skipping creation", orderId);
            return 0;
        }

        var orderItems = await _orderServiceClient.GetOrderItemsAsync(orderId, cancellationToken);
        if (orderItems.Count == 0)
        {
            _logger.LogWarning("No items found for OrderId: {OrderId}", orderId);
            return 0;
        }

        var now = DateTime.UtcNow;

        foreach (var item in orderItems)
        {
            var priority = CalculatePriority(item.DeliveryDate);

            var estimatedPrintTime = item.EstimatedPrintTimeMinutes > 0
                ? item.EstimatedPrintTimeMinutes
                : _timeEstimation.EstimatePrintTimeMinutes(item.Technology, item.VolumeCm3);

            var job = new Job
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                OrderItemId = item.OrderItemId,
                MaterialId = item.MaterialId,
                Technology = item.Technology,
                VolumeCm3 = item.VolumeCm3,
                EstimatedPrintTimeMinutes = estimatedPrintTime,
                SetupTimeMinutes = _timeEstimation.EstimateSetupTimeMinutes(item.Technology),
                Priority = priority,
                Status = JobStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _dbContext.Jobs.Add(job);

            _logger.LogInformation(
                "Created job {JobId} for OrderId: {OrderId}, OrderItemId: {OrderItemId}, Priority: {Priority}",
                job.Id,
                orderId,
                item.OrderItemId,
                priority);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _metrics.RecordJobCreated(orderItems.Count);

        _logger.LogInformation("Successfully created {Count} jobs for OrderId: {OrderId}", orderItems.Count, orderId);

        return orderItems.Count;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetQueueDepthByTechnologyAsync(string? technology, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[] { JobStatus.Queued, JobStatus.InProgress };

        var query = _dbContext.Jobs
            .AsNoTracking()
            .Where(j => activeStatuses.Contains(j.Status));

        if (!string.IsNullOrWhiteSpace(technology))
        {
            query = query.Where(j => j.Technology == technology);
        }

        var result = await query
            .GroupBy(j => j.Technology)
            .Select(g => new { Technology = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return result.ToDictionary(r => r.Technology, r => r.Count);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetMachineScheduleAsync(
        string machineId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => _schedulingService.GetMachineScheduleAsync(machineId, from, to, cancellationToken);

    /// <inheritdoc />
    public async Task<JobOperationResult> ReorderAsync(Guid id, int newPosition, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
            return JobOperationResult.NotFound();

        if (job.Status != JobStatus.Queued)
            return JobOperationResult.Failure("Can only reorder jobs in Queued status");

        if (string.IsNullOrEmpty(job.AssignedMachineId))
            return JobOperationResult.Failure("Job has no assigned machine");

        await _schedulingService.ReorderJobAsync(id, newPosition, job.AssignedMachineId, cancellationToken);

        // Reload the job to return updated fields
        await _dbContext.Entry(job).ReloadAsync(cancellationToken);
        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<int> UpdateOutsourcingStatusAsync(Guid orderId, bool isOutsourced, CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.Jobs
            .Where(j => j.OrderId == orderId)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            _logger.LogInformation("No jobs found for OrderId: {OrderId} to update outsourcing status", orderId);
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var job in jobs)
        {
            job.IsOutsourced = isOutsourced;
            job.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated outsourcing status to {IsOutsourced} for {Count} jobs on OrderId: {OrderId}",
            isOutsourced,
            jobs.Count,
            orderId);

        return jobs.Count;
    }

    private static int CalculatePriority(DateTime? deliveryDate)
    {
        if (!deliveryDate.HasValue)
        {
            return JobServiceConstants.DefaultPriorityForNoDelivery;
        }

        var daysRemaining = (deliveryDate.Value - DateTime.UtcNow).TotalDays;
        return Math.Max(0, (int)Math.Floor(daysRemaining));
    }

    private static (bool Valid, string Error) ValidateTransition(JobStatus currentStatus, string action)
    {
        bool isValid = currentStatus switch
        {
            JobStatus.Pending => action is "queue" or "start" or "cancel",
            JobStatus.Queued => action is "start" or "reassign" or "cancel",
            JobStatus.InProgress => action is "finish" or "cancel",
            JobStatus.Finishing => action is "complete" or "cancel",
            JobStatus.Completed => action is "cancel",
            JobStatus.Cancelled => false,
            _ => false,
        };

        if (isValid)
        {
            return (true, string.Empty);
        }

        return (false, $"Cannot {action} a job in {currentStatus} status");
    }

    private async Task PublishJobStatusChangedAsync(
        Job job,
        JobStatus previousStatus,
        string changedBy,
        CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(new JobStatusChangedEvent(
            MessageId: Guid.NewGuid(),
            MessageName: nameof(JobStatusChangedEvent),
            MessageType: MessageType.Event,
            MessageVersion: "1.0.0",
            PublishedBy: "job-service",
            ConsumedBy: Array.Empty<string>(),
            CorrelationId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            IsPublic: false,
            Payload: new JobStatusChangedEventPayload
            {
                JobId = job.Id,
                OrderId = job.OrderId,
                PreviousStatus = previousStatus.ToString(),
                NewStatus = job.Status.ToString(),
                Technology = job.Technology,
                AssignedMachineId = job.AssignedMachineId,
                ChangedAt = DateTime.UtcNow,
                ChangedBy = changedBy,
            }),
            cancellationToken);
    }
}
