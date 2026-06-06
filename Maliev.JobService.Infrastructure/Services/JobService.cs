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
    private static readonly TimeSpan QuietGap = TimeSpan.FromHours(1);
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
    public async Task<JobOperationResult> UpdateDetailsAsync(
        Guid id,
        UpdateJobDetailsCommand command,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        if (command.MaterialId == Guid.Empty)
        {
            return JobOperationResult.Failure("MaterialId must be a valid material identifier.");
        }

        if (command.Priority is < 0 or > 999)
        {
            return JobOperationResult.Failure("Priority must be between 0 and 999.");
        }

        if (command.MaterialId.HasValue)
        {
            job.MaterialId = command.MaterialId.Value;
        }

        if (command.Priority.HasValue)
        {
            job.Priority = command.Priority.Value;
        }

        job.CustomerId = NormalizeOptionalText(command.CustomerId);
        job.CustomerName = NormalizeOptionalText(command.CustomerName);
        job.AssignedOperator = NormalizeOptionalText(command.AssignedOperator);
        job.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated job {JobId} production details by {ChangedBy}",
            job.Id,
            changedBy);

        return JobOperationResult.Success(job);
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

            var quantity = Math.Max(1, item.Quantity);
            var estimatedPrintTime = item.EstimatedPrintTimeMinutes > 0
                ? item.EstimatedPrintTimeMinutes * quantity
                : _timeEstimation.EstimatePrintTimeMinutes(item.Technology, item.VolumeCm3 * quantity);

            var job = new Job
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                OrderItemId = item.OrderItemId,
                SourceProjectId = item.SourceProjectId,
                SourceProjectPartId = item.SourceProjectPartId,
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

            await ApplyMatchingPlanningHoldAsync(job, item, now, cancellationToken);
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
        var now = DateTime.UtcNow;
        await ExpirePlanningHoldsAsync(now, cancellationToken);

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

        var counts = result.ToDictionary(r => r.Technology, r => r.Count);

        var holdQuery = _dbContext.ProductionPlanningHolds
            .AsNoTracking()
            .Where(hold => hold.Status == PlanningHoldStatus.Active && hold.ExpiresAt > now);

        if (!string.IsNullOrWhiteSpace(technology))
        {
            holdQuery = holdQuery.Where(hold => hold.Technology == technology);
        }

        var holdCounts = await holdQuery
            .GroupBy(hold => hold.Technology)
            .Select(group => new { Technology = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        foreach (var holdCount in holdCounts)
        {
            counts[holdCount.Technology] = counts.GetValueOrDefault(holdCount.Technology) + holdCount.Count;
        }

        return counts;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Job>> GetMachineScheduleAsync(
        string machineId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => _schedulingService.GetMachineScheduleAsync(machineId, from, to, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductionScheduleSlot>> GetScheduleAsync(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<string>? machineIds = null,
        IReadOnlyCollection<string>? technologies = null,
        CancellationToken cancellationToken = default)
    {
        var rangeFrom = EnsureUtc(from);
        var rangeTo = EnsureUtc(to);
        if (rangeTo <= rangeFrom)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        await ExpirePlanningHoldsAsync(now, cancellationToken);

        var machineFilter = NormalizeFilter(machineIds);
        var technologyFilter = NormalizeFilter(technologies);

        var jobsQuery = _dbContext.Jobs
            .AsNoTracking()
            .Where(job =>
                job.AssignedMachineId != null &&
                job.ScheduledStartTime.HasValue &&
                job.ScheduledEndTime.HasValue &&
                job.ScheduledEndTime.Value > rangeFrom &&
                job.ScheduledStartTime.Value < rangeTo);

        if (machineFilter.Count > 0)
        {
            jobsQuery = jobsQuery.Where(job => machineFilter.Contains(job.AssignedMachineId!));
        }

        if (technologyFilter.Count > 0)
        {
            jobsQuery = jobsQuery.Where(job => technologyFilter.Contains(job.Technology));
        }

        var jobSlots = await jobsQuery
            .Select(job => new ProductionScheduleSlot
            {
                SlotId = job.Id,
                JobId = job.Id,
                MachineId = job.AssignedMachineId!,
                Technology = job.Technology,
                ScheduledStart = job.ScheduledStartTime!.Value,
                ScheduledEnd = job.ScheduledEndTime!.Value,
                SetupMinutes = job.SetupTimeMinutes,
                ProductionMinutes = job.EstimatedPrintTimeMinutes,
                QueuePosition = job.QueuePosition,
                Status = job.Status.ToString(),
                OrderId = job.OrderId,
                ProjectId = job.SourceProjectId,
                ProjectPartId = job.SourceProjectPartId,
            })
            .ToListAsync(cancellationToken);

        var holdsQuery = _dbContext.ProductionPlanningHolds
            .AsNoTracking()
            .Where(hold =>
                hold.Status == PlanningHoldStatus.Active &&
                hold.ExpiresAt > now &&
                hold.ScheduledEndTime > rangeFrom &&
                hold.ScheduledStartTime < rangeTo);

        if (machineFilter.Count > 0)
        {
            holdsQuery = holdsQuery.Where(hold => machineFilter.Contains(hold.MachineId));
        }

        if (technologyFilter.Count > 0)
        {
            holdsQuery = holdsQuery.Where(hold => technologyFilter.Contains(hold.Technology));
        }

        var holdSlots = await holdsQuery
            .Select(hold => new ProductionScheduleSlot
            {
                SlotId = hold.Id,
                JobId = hold.ConvertedJobId,
                HoldId = hold.Id,
                MachineId = hold.MachineId,
                MachineName = hold.MachineName,
                Technology = hold.Technology,
                ScheduledStart = hold.ScheduledStartTime,
                ScheduledEnd = hold.ScheduledEndTime,
                SetupMinutes = hold.SetupTimeMinutes,
                ProductionMinutes = hold.ProductionTimeMinutes,
                QueuePosition = hold.QueuePosition,
                Status = "Planning Hold",
                ProjectId = hold.ProjectId,
                ProjectPartId = hold.ProjectPartId,
                ExpiresAt = hold.ExpiresAt,
                IsHold = true,
            })
            .ToListAsync(cancellationToken);

        return jobSlots
            .Concat(holdSlots)
            .OrderBy(slot => slot.MachineId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(slot => slot.ScheduledStart)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<JobOperationResult> RescheduleAsync(
        Guid id,
        RescheduleJobCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.Jobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            return JobOperationResult.NotFound();
        }

        if (job.Status != JobStatus.Queued)
        {
            return JobOperationResult.Failure("Only queued jobs can be rescheduled.");
        }

        if (string.IsNullOrWhiteSpace(command.MachineId))
        {
            return JobOperationResult.Failure("MachineId is required.");
        }

        var start = EnsureUtc(command.ScheduledStartTime);
        var end = command.ScheduledEndTime.HasValue
            ? EnsureUtc(command.ScheduledEndTime.Value)
            : start.AddMinutes(Math.Max(0, job.SetupTimeMinutes) + Math.Max(1, job.EstimatedPrintTimeMinutes));

        if (start <= DateTime.UtcNow)
        {
            return JobOperationResult.Failure("Scheduled start must be in the future.");
        }

        if (end <= start)
        {
            return JobOperationResult.Failure("Scheduled end must be after scheduled start.");
        }

        if (await HasScheduleOverlapAsync(id, null, command.MachineId, start, end, cancellationToken))
        {
            return JobOperationResult.Failure("The requested schedule slot overlaps existing jobs or holds, or does not keep at least 1 hour quiet gap.");
        }

        var oldMachineId = job.AssignedMachineId;
        job.AssignedMachineId = command.MachineId;
        job.ScheduledStartTime = start;
        job.ScheduledEndTime = end;
        if (command.QueuePosition is > 0)
        {
            job.QueuePosition = command.QueuePosition.Value;
        }
        else if (job.QueuePosition <= 0 || !string.Equals(oldMachineId, command.MachineId, StringComparison.OrdinalIgnoreCase))
        {
            job.QueuePosition = await ResolveJobQueuePositionAsync(command.MachineId, job.Id, cancellationToken);
        }

        job.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (command.CascadeFollowingJobs &&
            !string.IsNullOrWhiteSpace(oldMachineId) &&
            !string.Equals(oldMachineId, command.MachineId, StringComparison.OrdinalIgnoreCase))
        {
            await _schedulingService.RescheduleQueueAsync(oldMachineId, cancellationToken);
        }

        _logger.LogInformation(
            "Rescheduled job {JobId} on machine {MachineId}: {Start:u} - {End:u}",
            job.Id,
            command.MachineId,
            start,
            end);

        return JobOperationResult.Success(job);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductionPlanningHold>> GetPlanningHoldsAsync(
        Guid? projectId,
        string? technology,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await ExpirePlanningHoldsAsync(now, cancellationToken);

        var query = _dbContext.ProductionPlanningHolds.AsNoTracking();

        if (projectId.HasValue)
        {
            query = query.Where(hold => hold.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            query = query.Where(hold => hold.Technology == technology);
        }

        if (activeOnly)
        {
            query = query.Where(hold => hold.Status == PlanningHoldStatus.Active && hold.ExpiresAt > now);
        }

        return await query
            .OrderBy(hold => hold.MachineId)
            .ThenBy(hold => hold.QueuePosition)
            .ThenBy(hold => hold.ScheduledStartTime)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PlanningHoldOperationResult> CreatePlanningHoldAsync(
        CreatePlanningHoldCommand command,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePlanningHold(command.ProjectId, command.ProjectPartId, command.Technology, command.MachineId, command.ExpiresAt);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return PlanningHoldOperationResult.Failure(validationError);
        }

        var now = DateTime.UtcNow;
        await ExpirePlanningHoldsAsync(now, cancellationToken);

        var existing = await _dbContext.ProductionPlanningHolds
            .FirstOrDefaultAsync(hold =>
                hold.ProjectId == command.ProjectId &&
                hold.ProjectPartId == command.ProjectPartId &&
                hold.Status == PlanningHoldStatus.Active,
                cancellationToken);

        if (existing is not null)
        {
            return PlanningHoldOperationResult.Failure("An active planning hold already exists for this project part.");
        }

        var start = EnsureUtc(command.ScheduledStartTime);
        var end = ResolveHoldEnd(start, command.ScheduledEndTime, command.SetupTimeMinutes, command.ProductionTimeMinutes);
        if (end <= start)
        {
            return PlanningHoldOperationResult.Failure("Scheduled end must be after scheduled start.");
        }

        if (await HasScheduleOverlapAsync(null, null, command.MachineId, start, end, cancellationToken))
        {
            if (command.AutoSnap)
            {
                var snapped = await FindNextAvailableSlotAsync(command.MachineId, start, end, cancellationToken);
                if (snapped == null)
                {
                    return PlanningHoldOperationResult.Failure("No available slot found on this machine within the next 30 days.");
                }

                start = snapped.Value.start;
                end = snapped.Value.end;
                _logger.LogInformation("Planning hold auto-snapped to next available slot: {MachineId} {Start:O} – {End:O}", command.MachineId, start, end);
            }
            else
            {
                return PlanningHoldOperationResult.Failure("The requested planning hold must keep at least 1 hour quiet gap from existing jobs or holds.");
            }
        }

        var hold = new ProductionPlanningHold
        {
            Id = Guid.NewGuid(),
            ProjectId = command.ProjectId,
            ProjectPartId = command.ProjectPartId,
            Technology = command.Technology,
            MachineId = command.MachineId,
            MachineName = command.MachineName,
            QueuePosition = await ResolveHoldQueuePositionAsync(command.MachineId, command.QueuePosition, cancellationToken),
            ScheduledStartTime = start,
            ScheduledEndTime = end,
            SetupTimeMinutes = Math.Max(0, command.SetupTimeMinutes),
            ProductionTimeMinutes = Math.Max(1, command.ProductionTimeMinutes),
            Quantity = Math.Max(1, command.Quantity),
            Status = PlanningHoldStatus.Active,
            Notes = command.Notes,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = EnsureUtc(command.ExpiresAt)
        };

        _dbContext.ProductionPlanningHolds.Add(hold);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created production planning hold {HoldId} for ProjectPart {ProjectPartId} on machine {MachineId}",
            hold.Id,
            hold.ProjectPartId,
            hold.MachineId);

        return PlanningHoldOperationResult.Success(hold);
    }

    /// <inheritdoc />
    public async Task<PlanningHoldOperationResult> UpdatePlanningHoldAsync(
        Guid id,
        UpdatePlanningHoldCommand command,
        CancellationToken cancellationToken = default)
    {
        var hold = await _dbContext.ProductionPlanningHolds.FindAsync([id], cancellationToken);
        if (hold is null)
        {
            return PlanningHoldOperationResult.NotFound();
        }

        if (hold.Status != PlanningHoldStatus.Active)
        {
            return PlanningHoldOperationResult.Failure("Only active planning holds can be updated.");
        }

        var validationError = ValidatePlanningHold(hold.ProjectId, hold.ProjectPartId, hold.Technology, command.MachineId, command.ExpiresAt);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return PlanningHoldOperationResult.Failure(validationError);
        }

        var start = EnsureUtc(command.ScheduledStartTime);
        var end = ResolveHoldEnd(start, command.ScheduledEndTime, command.SetupTimeMinutes, command.ProductionTimeMinutes);
        if (end <= start)
        {
            return PlanningHoldOperationResult.Failure("Scheduled end must be after scheduled start.");
        }

        if (await HasScheduleOverlapAsync(null, hold.Id, command.MachineId, start, end, cancellationToken))
        {
            return PlanningHoldOperationResult.Failure("The requested planning hold must keep at least 1 hour quiet gap from existing jobs or holds.");
        }

        hold.MachineId = command.MachineId;
        hold.MachineName = command.MachineName;
        hold.QueuePosition = await ResolveHoldQueuePositionAsync(command.MachineId, command.QueuePosition, cancellationToken, hold.Id);
        hold.ScheduledStartTime = start;
        hold.ScheduledEndTime = end;
        hold.SetupTimeMinutes = Math.Max(0, command.SetupTimeMinutes);
        hold.ProductionTimeMinutes = Math.Max(1, command.ProductionTimeMinutes);
        hold.Notes = command.Notes;
        hold.ExpiresAt = EnsureUtc(command.ExpiresAt);
        hold.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return PlanningHoldOperationResult.Success(hold);
    }

    /// <inheritdoc />
    public async Task<PlanningHoldOperationResult> CancelPlanningHoldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hold = await _dbContext.ProductionPlanningHolds.FindAsync([id], cancellationToken);
        if (hold is null)
        {
            return PlanningHoldOperationResult.NotFound();
        }

        if (hold.Status != PlanningHoldStatus.Active)
        {
            return PlanningHoldOperationResult.Failure("Only active planning holds can be cancelled.");
        }

        hold.Status = PlanningHoldStatus.Cancelled;
        hold.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return PlanningHoldOperationResult.Success(hold);
    }

    /// <inheritdoc />
    public async Task<int> ExpirePlanningHoldsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var now = EnsureUtc(utcNow);
        var expired = await _dbContext.ProductionPlanningHolds
            .Where(hold => hold.Status == PlanningHoldStatus.Active && hold.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var hold in expired)
        {
            hold.Status = PlanningHoldStatus.Expired;
            hold.UpdatedAt = now;
        }

        if (expired.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {Count} production planning holds", expired.Count);
        }

        return expired.Count;
    }

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

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task ApplyMatchingPlanningHoldAsync(
        Job job,
        Maliev.JobService.Domain.Models.OrderItemDto item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!item.SourceProjectId.HasValue || !item.SourceProjectPartId.HasValue)
        {
            return;
        }

        var hold = await _dbContext.ProductionPlanningHolds
            .FirstOrDefaultAsync(candidate =>
                candidate.ProjectId == item.SourceProjectId.Value &&
                candidate.ProjectPartId == item.SourceProjectPartId.Value &&
                candidate.Status == PlanningHoldStatus.Active &&
                candidate.ExpiresAt > now,
                cancellationToken);

        if (hold is null)
        {
            return;
        }

        job.AssignedMachineId = hold.MachineId;
        job.QueuePosition = hold.QueuePosition;
        job.ScheduledStartTime = hold.ScheduledStartTime;
        job.ScheduledEndTime = hold.ScheduledEndTime;

        hold.Status = PlanningHoldStatus.Converted;
        hold.ConvertedJobId = job.Id;
        hold.UpdatedAt = now;
    }

    private async Task<int> ResolveHoldQueuePositionAsync(
        string machineId,
        int requestedPosition,
        CancellationToken cancellationToken,
        Guid? excludedHoldId = null)
    {
        if (requestedPosition > 0)
        {
            return requestedPosition;
        }

        var activeJobCount = await _dbContext.Jobs
            .CountAsync(job =>
                job.AssignedMachineId == machineId &&
                (job.Status == JobStatus.Queued || job.Status == JobStatus.InProgress),
                cancellationToken);

        var holdQuery = _dbContext.ProductionPlanningHolds
            .Where(hold => hold.MachineId == machineId && hold.Status == PlanningHoldStatus.Active);

        if (excludedHoldId.HasValue)
        {
            holdQuery = holdQuery.Where(hold => hold.Id != excludedHoldId.Value);
        }

        var activeHoldCount = await holdQuery.CountAsync(cancellationToken);
        return activeJobCount + activeHoldCount + 1;
    }

    private async Task<int> ResolveJobQueuePositionAsync(
        string machineId,
        Guid excludedJobId,
        CancellationToken cancellationToken)
    {
        var activeJobCount = await _dbContext.Jobs
            .CountAsync(job =>
                job.Id != excludedJobId &&
                job.AssignedMachineId == machineId &&
                (job.Status == JobStatus.Queued || job.Status == JobStatus.InProgress),
                cancellationToken);

        var activeHoldCount = await _dbContext.ProductionPlanningHolds
            .CountAsync(hold =>
                hold.MachineId == machineId &&
                hold.Status == PlanningHoldStatus.Active,
                cancellationToken);

        return activeJobCount + activeHoldCount + 1;
    }

    private async Task<bool> HasScheduleOverlapAsync(
        Guid? excludedJobId,
        Guid? excludedHoldId,
        string machineId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var bufferedStart = start.Subtract(QuietGap);
        var bufferedEnd = end.Add(QuietGap);

        if (await HasJobOverlapAsync(excludedJobId, machineId, bufferedStart, bufferedEnd, cancellationToken))
            return true;

        var now = DateTime.UtcNow;
        return await _dbContext.ProductionPlanningHolds
            .AsNoTracking()
            .AnyAsync(hold =>
                (!excludedHoldId.HasValue || hold.Id != excludedHoldId.Value) &&
                hold.MachineId == machineId &&
                hold.Status == PlanningHoldStatus.Active &&
                hold.ExpiresAt > now &&
                hold.ScheduledStartTime < bufferedEnd &&
                hold.ScheduledEndTime > bufferedStart,
                cancellationToken);
    }

    /// <summary>
    /// Scans forward from <paramref name="requestedStart"/> to find the earliest slot
    /// where the hold can be placed without overlapping existing jobs or holds.
    /// Returns null if no slot is found within 30 days.
    /// </summary>
    private async Task<(DateTime start, DateTime end)?> FindNextAvailableSlotAsync(
        string machineId,
        DateTime requestedStart,
        DateTime requestedEnd,
        CancellationToken cancellationToken)
    {
        var duration = requestedEnd - requestedStart;
        var searchLimit = requestedStart.AddDays(30);
        var activeStatuses = new[] { JobStatus.Queued, JobStatus.InProgress, JobStatus.Finishing };

        // Gather all blocking schedule entries (jobs + holds) ordered by their buffered end time
        var jobs = await _dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedMachineId == machineId
                     && activeStatuses.Contains(j.Status)
                     && j.ScheduledStartTime.HasValue
                     && j.ScheduledEndTime.HasValue
                     && j.ScheduledEndTime.Value > requestedStart.Subtract(QuietGap))
            .OrderBy(j => j.ScheduledStartTime)
            .Select(j => new { j.ScheduledStartTime, j.ScheduledEndTime })
            .ToListAsync(cancellationToken);

        var holds = await _dbContext.ProductionPlanningHolds
            .AsNoTracking()
            .Where(h => h.MachineId == machineId
                     && h.Status == PlanningHoldStatus.Active
                     && h.ExpiresAt > DateTime.UtcNow
                     && h.ScheduledEndTime > requestedStart.Subtract(QuietGap))
            .OrderBy(h => h.ScheduledStartTime)
            .Select(h => new { h.ScheduledStartTime, h.ScheduledEndTime })
            .ToListAsync(cancellationToken);

        // Merge both lists into a sorted timeline of blocking intervals (with quiet gap added)
        var blocks = jobs
            .Select(j => (start: j.ScheduledStartTime!.Value, end: j.ScheduledEndTime!.Value.Add(QuietGap)))
            .Concat(holds.Select(h => (start: h.ScheduledStartTime, end: h.ScheduledEndTime.Add(QuietGap))))
            .OrderBy(b => b.start)
            .ToList();

        // Merge overlapping blocks into contiguous busy periods
        var merged = new List<(DateTime start, DateTime end)>();
        foreach (var block in blocks)
        {
            if (merged.Count == 0 || block.start > merged[^1].end)
            {
                merged.Add(block);
            }
            else if (block.end > merged[^1].end)
            {
                merged[^1] = (merged[^1].start, block.end);
            }
        }

        // Walk through gaps between merged blocks to find a slot big enough
        var cursor = requestedStart;
        foreach (var block in merged)
        {
            var candidateEnd = cursor + duration;
            if (candidateEnd <= block.start && cursor + QuietGap <= block.start)
            {
                // The gap before this block is large enough
                return (cursor, candidateEnd);
            }

            // Move cursor past this block (plus quiet gap)
            cursor = block.end > cursor ? block.end : cursor;
        }

        // Check the gap after the last block
        var finalEnd = cursor + duration;
        if (finalEnd <= searchLimit)
        {
            return (cursor, finalEnd);
        }

        return null;
    }

    private async Task<bool> HasJobOverlapAsync(
        Guid? excludedJobId,
        string machineId,
        DateTime bufferedStart,
        DateTime bufferedEnd,
        CancellationToken cancellationToken)
    {
        var activeStatuses = new[] { JobStatus.Queued, JobStatus.InProgress, JobStatus.Finishing };
        return await _dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(job =>
                (!excludedJobId.HasValue || job.Id != excludedJobId.Value) &&
                job.AssignedMachineId == machineId &&
                activeStatuses.Contains(job.Status) &&
                job.ScheduledStartTime.HasValue &&
                job.ScheduledEndTime.HasValue &&
                job.ScheduledStartTime.Value < bufferedEnd &&
                job.ScheduledEndTime.Value > bufferedStart,
                cancellationToken);
    }

    private static HashSet<string> NormalizeFilter(IReadOnlyCollection<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return values
            .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ValidatePlanningHold(Guid projectId, Guid projectPartId, string technology, string machineId, DateTime expiresAt)
    {
        if (projectId == Guid.Empty)
        {
            return "ProjectId is required.";
        }

        if (projectPartId == Guid.Empty)
        {
            return "ProjectPartId is required.";
        }

        if (string.IsNullOrWhiteSpace(technology))
        {
            return "Technology is required.";
        }

        if (string.IsNullOrWhiteSpace(machineId))
        {
            return "MachineId is required.";
        }

        if (EnsureUtc(expiresAt) <= DateTime.UtcNow)
        {
            return "Expiration must be in the future.";
        }

        return null;
    }

    private static DateTime ResolveHoldEnd(DateTime start, DateTime? requestedEnd, int setupMinutes, int productionMinutes)
    {
        if (requestedEnd.HasValue)
        {
            var utcEnd = EnsureUtc(requestedEnd.Value);
            if (utcEnd > start)
            {
                return utcEnd;
            }
        }

        return start.AddMinutes(Math.Max(0, setupMinutes) + Math.Max(1, productionMinutes));
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind).ToUniversalTime();
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
