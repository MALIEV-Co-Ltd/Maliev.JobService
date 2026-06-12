namespace Maliev.JobService.Domain.Entities;

/// <summary>
/// Represents a durable audit record for a manufacturing job status transition.
/// </summary>
public class JobStatusTransitionAudit
{
    /// <summary>
    /// Gets or sets the audit record identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the job identifier.
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Gets or sets the status before the transition.
    /// </summary>
    public JobStatus PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the status after the transition.
    /// </summary>
    public JobStatus NewStatus { get; set; }

    /// <summary>
    /// Gets or sets the operator or service principal that made the transition.
    /// </summary>
    public required string ChangedBy { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the transition happened.
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets optional transition notes.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the related job.
    /// </summary>
    public Job? Job { get; set; }
}
