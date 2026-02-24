namespace Maliev.JobService.Data.Entities;

/// <summary>
/// Defines the possible states of a manufacturing job.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// The job has been created but not yet scheduled.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The job has been assigned to a machine and is waiting in the queue.
    /// </summary>
    Queued = 1,

    /// <summary>
    /// The job is currently being processed on a machine.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The job is in the post-processing stage (e.g., support removal, sanding).
    /// </summary>
    Finishing = 3,

    /// <summary>
    /// The job is finished and ready for shipping.
    /// </summary>
    Completed = 4,

    /// <summary>
    /// The job was cancelled.
    /// </summary>
    Cancelled = 5
}
