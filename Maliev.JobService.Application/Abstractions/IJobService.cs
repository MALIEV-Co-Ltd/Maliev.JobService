using Maliev.JobService.Application.Models;
using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Application.Abstractions;

/// <summary>
/// Defines application-level operations for managing manufacturing jobs.
/// </summary>
public interface IJobService
{
    /// <summary>
    /// Gets a job by identifier.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching job, or <see langword="null"/> when not found.</returns>
    Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the durable status transition audit trail for a job.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The ordered audit records for the job.</returns>
    Task<IReadOnlyList<JobStatusTransitionAudit>> GetStatusTransitionAuditsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of jobs with optional filters.
    /// </summary>
    /// <param name="status">The optional status filter.</param>
    /// <param name="technology">The optional technology filter.</param>
    /// <param name="assignedMachineId">The optional assigned machine filter.</param>
    /// <param name="page">The 1-based page index.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A paged set of jobs.</returns>
    Task<PagedResult<Job>> GetJobsAsync(
        JobStatus? status,
        string? technology,
        string? assignedMachineId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all jobs linked to an order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The jobs linked to the order.</returns>
    Task<IReadOnlyList<Job>> GetJobsByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active and recently-updated jobs for Kanban display.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The jobs for Kanban view.</returns>
    Task<IReadOnlyList<Job>> GetKanbanJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates editable production details on a job.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="command">The requested detail changes.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> UpdateDetailsAsync(
        Guid id,
        UpdateJobDetailsCommand command,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a job to the queued status and assigns a machine.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> QueueAsync(
        Guid id,
        string machineId,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a queued or pending job.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> StartAsync(Guid id, string changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an in-progress job to finishing.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> FinishAsync(Guid id, string changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a finishing job.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> CompleteAsync(Guid id, string changedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a job.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="reason">The cancellation reason.</param>
    /// <param name="changedBy">The user performing the action.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> CancelAsync(
        Guid id,
        string reason,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reassigns a queued job to another machine.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="machineId">The target machine identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> ReassignAsync(Guid id, string machineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates jobs for a paid order.
    /// </summary>
    /// <param name="orderId">The paid order identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of created jobs.</returns>
    Task<int> CreateJobsForPaidOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates jobs for a paid order using its human-readable order number for downstream lookups.
    /// </summary>
    /// <param name="orderId">The paid order event identifier.</param>
    /// <param name="orderNumber">The human-readable order number.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of created jobs.</returns>
    Task<int> CreateJobsForPaidOrderAsync(Guid orderId, string orderNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the queue depth (number of active jobs) by technology.
    /// </summary>
    /// <param name="technology">The manufacturing technology filter (e.g., FDM, SLA, CNC).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A dictionary with technology as key and count of active (Queued + InProgress) jobs as value.</returns>
    Task<Dictionary<string, int>> GetQueueDepthByTechnologyAsync(string? technology, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets production planning holds with optional project and technology filters.
    /// </summary>
    /// <param name="projectId">Optional source project filter.</param>
    /// <param name="technology">Optional manufacturing technology filter.</param>
    /// <param name="activeOnly">True to return only active non-expired holds.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching planning holds.</returns>
    Task<IReadOnlyList<ProductionPlanningHold>> GetPlanningHoldsAsync(
        Guid? projectId,
        string? technology,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one production planning hold by its identifier.
    /// </summary>
    /// <param name="id">The planning hold identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching planning hold, or <see langword="null"/> when it does not exist.</returns>
    Task<ProductionPlanningHold?> GetPlanningHoldAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tentative production planning hold.
    /// </summary>
    /// <param name="command">The create command.</param>
    /// <param name="createdBy">The user creating the hold.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<PlanningHoldOperationResult> CreatePlanningHoldAsync(
        CreatePlanningHoldCommand command,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an active tentative production planning hold.
    /// </summary>
    /// <param name="id">The hold identifier.</param>
    /// <param name="command">The update command.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<PlanningHoldOperationResult> UpdatePlanningHoldAsync(
        Guid id,
        UpdatePlanningHoldCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active tentative production planning hold.
    /// </summary>
    /// <param name="id">The hold identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<PlanningHoldOperationResult> CancelPlanningHoldAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires active planning holds whose expiration timestamp has passed.
    /// </summary>
    /// <param name="utcNow">The current UTC timestamp.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of holds expired.</returns>
    Task<int> ExpirePlanningHoldsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the outsourcing status of all jobs belonging to a given order.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="isOutsourced">The new outsourcing state.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of jobs updated.</returns>
    Task<int> UpdateOutsourcingStatusAsync(Guid orderId, bool isOutsourced, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all scheduled jobs on a specific machine within a UTC date range.
    /// </summary>
    /// <param name="machineId">The machine identifier.</param>
    /// <param name="from">Range start (UTC).</param>
    /// <param name="to">Range end (UTC).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<IReadOnlyList<Job>> GetMachineScheduleAsync(
        string machineId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets scheduled production jobs and active planning holds across machines.
    /// </summary>
    /// <param name="from">Range start in UTC.</param>
    /// <param name="to">Range end in UTC.</param>
    /// <param name="machineIds">Optional machine identifiers to include.</param>
    /// <param name="technologies">Optional manufacturing technologies to include.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching schedule slots.</returns>
    Task<IReadOnlyList<ProductionScheduleSlot>> GetScheduleAsync(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<string>? machineIds = null,
        IReadOnlyCollection<string>? technologies = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a queued job to a specific machine schedule slot.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="command">The requested schedule move.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> RescheduleAsync(
        Guid id,
        RescheduleJobCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders a queued job to a new position and cascades rescheduling.
    /// </summary>
    /// <param name="id">The job identifier.</param>
    /// <param name="newPosition">The target queue position (1-based).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<JobOperationResult> ReorderAsync(Guid id, int newPosition, CancellationToken cancellationToken = default);
}
