namespace Maliev.JobService.Application.Models;

/// <summary>
/// Command to create a tentative production planning hold.
/// </summary>
public sealed record CreatePlanningHoldCommand
{
    /// <summary>Gets the source project identifier.</summary>
    public Guid ProjectId { get; init; }

    /// <summary>Gets the source project part identifier.</summary>
    public Guid ProjectPartId { get; init; }

    /// <summary>Gets the manufacturing technology or process code.</summary>
    public string Technology { get; init; } = string.Empty;

    /// <summary>Gets the target machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the target machine display name.</summary>
    public string? MachineName { get; init; }

    /// <summary>Gets the requested queue position. Zero appends to the current machine queue.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC, if the caller has a planned end.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupTimeMinutes { get; init; }

    /// <summary>Gets the production run time in minutes.</summary>
    public int ProductionTimeMinutes { get; init; }

    /// <summary>Gets the quoted part quantity covered by this hold.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets optional planning notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the UTC expiration timestamp.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether the scheduler should automatically snap the hold
    /// to the next available slot (after the quiet gap) when the requested time conflicts
    /// with existing schedule entries.
    /// </summary>
    public bool AutoSnap { get; init; }
}

/// <summary>
/// Command to update an active production planning hold.
/// </summary>
public sealed record UpdatePlanningHoldCommand
{
    /// <summary>Gets the target machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the target machine display name.</summary>
    public string? MachineName { get; init; }

    /// <summary>Gets the requested queue position. Zero appends to the current machine queue.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC, if the caller has a planned end.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupTimeMinutes { get; init; }

    /// <summary>Gets the production run time in minutes.</summary>
    public int ProductionTimeMinutes { get; init; }

    /// <summary>Gets optional planning notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the UTC expiration timestamp.</summary>
    public DateTime ExpiresAt { get; init; }
}
