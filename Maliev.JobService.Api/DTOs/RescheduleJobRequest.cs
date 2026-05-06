using Maliev.JobService.Application.Models;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Request DTO for moving a queued job to a schedule slot.
/// </summary>
public sealed record RescheduleJobRequest
{
    /// <summary>Gets the target machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC, if the caller has one.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the optional target queue position.</summary>
    public int? QueuePosition { get; init; }

    /// <summary>Gets a value indicating whether following queued jobs should be compacted after the move.</summary>
    public bool CascadeFollowingJobs { get; init; }

    /// <summary>Maps this request to an application command.</summary>
    /// <returns>The application command.</returns>
    public RescheduleJobCommand ToCommand() => new()
    {
        MachineId = MachineId,
        ScheduledStartTime = ScheduledStartTime,
        ScheduledEndTime = ScheduledEndTime,
        QueuePosition = QueuePosition,
        CascadeFollowingJobs = CascadeFollowingJobs,
    };
}
