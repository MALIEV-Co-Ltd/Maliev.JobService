namespace Maliev.JobService.Domain.Entities;

/// <summary>
/// Represents a tentative capacity reservation for a quoted project part.
/// </summary>
public class ProductionPlanningHold
{
    /// <summary>Gets or sets the hold identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the project identifier that owns the quoted part.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Gets or sets the quoted project part identifier.</summary>
    public Guid ProjectPartId { get; set; }

    /// <summary>Gets or sets the manufacturing technology or process code.</summary>
    public required string Technology { get; set; }

    /// <summary>Gets or sets the machine identifier or asset code reserved by this hold.</summary>
    public required string MachineId { get; set; }

    /// <summary>Gets or sets the human-readable machine name captured for display.</summary>
    public string? MachineName { get; set; }

    /// <summary>Gets or sets the planned queue position on the selected machine.</summary>
    public int QueuePosition { get; set; }

    /// <summary>Gets or sets the planned start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; set; }

    /// <summary>Gets or sets the planned end time in UTC.</summary>
    public DateTime ScheduledEndTime { get; set; }

    /// <summary>Gets or sets the setup time reserved in minutes.</summary>
    public int SetupTimeMinutes { get; set; }

    /// <summary>Gets or sets the production run time reserved in minutes.</summary>
    public int ProductionTimeMinutes { get; set; }

    /// <summary>Gets or sets the part quantity covered by the hold.</summary>
    public int Quantity { get; set; }

    /// <summary>Gets or sets the current hold lifecycle status.</summary>
    public PlanningHoldStatus Status { get; set; }

    /// <summary>Gets or sets optional planning notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the user that created the hold.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp when the hold was created in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the timestamp when the hold was last updated in UTC.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the hold automatically expires.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the job created from this hold, if converted.</summary>
    public Guid? ConvertedJobId { get; set; }
}
