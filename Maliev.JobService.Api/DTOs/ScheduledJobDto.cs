using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// A compact view of a job's scheduling slot for calendar/timeline display.
/// </summary>
public record ScheduledJobDto
{
    /// <summary>Gets the job identifier.</summary>
    public required Guid JobId { get; init; }
    /// <summary>Gets the manufacturing technology.</summary>
    public required string Technology { get; init; }
    /// <summary>Gets the scheduled start time (UTC).</summary>
    public required DateTime ScheduledStart { get; init; }
    /// <summary>Gets the scheduled end time (UTC).</summary>
    public required DateTime ScheduledEnd { get; init; }
    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupMinutes { get; init; }
    /// <summary>Gets the estimated print/machining time in minutes.</summary>
    public int PrintMinutes { get; init; }
    /// <summary>Gets the position in the machine queue.</summary>
    public int QueuePosition { get; init; }
    /// <summary>Gets the job status.</summary>
    public required string Status { get; init; }
    /// <summary>Gets the order ID for cross-referencing.</summary>
    public Guid OrderId { get; init; }
    /// <summary>Gets a value indicating whether this schedule item is a tentative planning hold.</summary>
    public bool IsHold { get; init; }
    /// <summary>Gets the planning hold identifier when <see cref="IsHold"/> is true.</summary>
    public Guid? HoldId { get; init; }
    /// <summary>Gets the source project identifier for a planning hold or project-originated job.</summary>
    public Guid? ProjectId { get; init; }
    /// <summary>Gets the source project part identifier for a planning hold or project-originated job.</summary>
    public Guid? ProjectPartId { get; init; }
    /// <summary>Gets the planning hold expiration time when <see cref="IsHold"/> is true.</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Maps a Job entity to a ScheduledJobDto.</summary>
    public static ScheduledJobDto FromEntity(Job job) => new()
    {
        JobId = job.Id,
        Technology = job.Technology,
        ScheduledStart = job.ScheduledStartTime!.Value,
        ScheduledEnd = job.ScheduledEndTime!.Value,
        SetupMinutes = job.SetupTimeMinutes,
        PrintMinutes = job.EstimatedPrintTimeMinutes,
        QueuePosition = job.QueuePosition,
        Status = job.Status.ToString(),
        OrderId = job.OrderId,
        ProjectId = job.SourceProjectId,
        ProjectPartId = job.SourceProjectPartId,
    };

    /// <summary>Maps a planning hold entity to a ScheduledJobDto.</summary>
    public static ScheduledJobDto FromHold(ProductionPlanningHold hold) => new()
    {
        JobId = hold.ConvertedJobId ?? hold.Id,
        Technology = hold.Technology,
        ScheduledStart = hold.ScheduledStartTime,
        ScheduledEnd = hold.ScheduledEndTime,
        SetupMinutes = hold.SetupTimeMinutes,
        PrintMinutes = hold.ProductionTimeMinutes,
        QueuePosition = hold.QueuePosition,
        Status = "Planning Hold",
        OrderId = Guid.Empty,
        IsHold = true,
        HoldId = hold.Id,
        ProjectId = hold.ProjectId,
        ProjectPartId = hold.ProjectPartId,
        ExpiresAt = hold.ExpiresAt,
    };
}
