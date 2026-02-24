namespace Maliev.JobService.Data.Entities;

/// <summary>
/// Represents a manufacturing job on the shop floor.
/// </summary>
public class Job
{
    /// <summary>
    /// Gets or sets the unique identifier for the job.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the order ID this job belongs to.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Gets or sets the specific order item ID this job is producing.
    /// </summary>
    public Guid OrderItemId { get; set; }

    /// <summary>
    /// Gets or sets the material ID required for this job.
    /// </summary>
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Gets or sets the manufacturing technology (e.g., FDM, SLA, CNC).
    /// </summary>
    public required string Technology { get; set; }

    /// <summary>
    /// Gets or sets the calculated volume of the part in cubic centimeters.
    /// </summary>
    public decimal VolumeCm3 { get; set; }

    /// <summary>
    /// Gets or sets the estimated production time in minutes.
    /// </summary>
    public int EstimatedPrintTimeMinutes { get; set; }

    /// <summary>
    /// Gets or sets the ID of the machine assigned to this job.
    /// </summary>
    public string? AssignedMachineId { get; set; }

    /// <summary>
    /// Gets or sets the production priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the current production status.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets optional production notes.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
