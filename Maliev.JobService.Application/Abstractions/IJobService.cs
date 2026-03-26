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
    /// Gets active and recently-updated jobs for Kanban display.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The jobs for Kanban view.</returns>
    Task<IReadOnlyList<Job>> GetKanbanJobsAsync(CancellationToken cancellationToken = default);

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
    /// Gets the queue depth (number of active jobs) by technology.
    /// </summary>
    /// <param name="technology">The manufacturing technology filter (e.g., FDM, SLA, CNC).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A dictionary with technology as key and count of active (Queued + InProgress) jobs as value.</returns>
    Task<Dictionary<string, int>> GetQueueDepthByTechnologyAsync(string? technology, CancellationToken cancellationToken = default);
}
