using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Application.Abstractions;

/// <summary>
/// Handles automated slot assignment and cascading rescheduling for manufacturing jobs.
/// </summary>
public interface ISchedulingService
{
    /// <summary>
    /// Computes and assigns a time slot for a newly queued job on the given machine.
    /// Appends the job after all existing Queued/InProgress jobs on that machine.
    /// </summary>
    Task ComputeSlotAsync(Job job, string machineId, CancellationToken ct = default);

    /// <summary>
    /// Cascades rescheduling for all remaining Queued jobs on a machine after a
    /// state change (completion, cancellation) frees up a time slot.
    /// </summary>
    Task RescheduleQueueAsync(string machineId, CancellationToken ct = default);

    /// <summary>
    /// Changes the queue position of a job and cascades rescheduling for all
    /// affected jobs on the same machine.
    /// </summary>
    Task ReorderJobAsync(Guid jobId, int newPosition, string machineId, CancellationToken ct = default);

    /// <summary>
    /// Gets all jobs scheduled on a specific machine within the given UTC date range.
    /// </summary>
    Task<IReadOnlyList<Job>> GetMachineScheduleAsync(string machineId, DateTime from, DateTime to, CancellationToken ct = default);
}
