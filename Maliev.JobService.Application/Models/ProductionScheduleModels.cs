namespace Maliev.JobService.Application.Models;

/// <summary>
/// Describes a job or tentative planning hold occupying capacity on a machine.
/// </summary>
public sealed record ProductionScheduleSlot
{
    /// <summary>Gets the stable slot identifier.</summary>
    public Guid SlotId { get; init; }

    /// <summary>Gets the job identifier when the slot represents a production job.</summary>
    public Guid? JobId { get; init; }

    /// <summary>Gets the planning hold identifier when the slot represents a hold.</summary>
    public Guid? HoldId { get; init; }

    /// <summary>Gets the scheduled machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the captured machine display name when available.</summary>
    public string? MachineName { get; init; }

    /// <summary>Gets the manufacturing technology or process code.</summary>
    public string Technology { get; init; } = string.Empty;

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStart { get; init; }

    /// <summary>Gets the scheduled end time in UTC.</summary>
    public DateTime ScheduledEnd { get; init; }

    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupMinutes { get; init; }

    /// <summary>Gets the production run time in minutes.</summary>
    public int ProductionMinutes { get; init; }

    /// <summary>Gets the queue position on the machine.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the lifecycle status label.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets the source order identifier for production jobs.</summary>
    public Guid? OrderId { get; init; }

    /// <summary>Gets the source project identifier when available.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>Gets the source project part identifier when available.</summary>
    public Guid? ProjectPartId { get; init; }

    /// <summary>Gets the planning hold expiration timestamp when applicable.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Gets a value indicating whether this slot is a tentative planning hold.</summary>
    public bool IsHold { get; init; }
}

/// <summary>
/// Command for moving a queued job to a machine schedule slot.
/// </summary>
public sealed record RescheduleJobCommand
{
    /// <summary>Gets the target machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the optional target queue position.</summary>
    public int? QueuePosition { get; init; }

    /// <summary>Gets a value indicating whether following queued jobs should be compacted after the move.</summary>
    public bool CascadeFollowingJobs { get; init; }
}
