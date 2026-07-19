using Maliev.JobService.Application.Models;
using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Response DTO for a tentative production planning hold.
/// </summary>
public sealed record ProductionPlanningHoldDto
{
    /// <summary>Gets the hold identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the source project identifier.</summary>
    public Guid ProjectId { get; init; }

    /// <summary>Gets the source project part identifier.</summary>
    public Guid ProjectPartId { get; init; }

    /// <summary>Gets the manufacturing technology or process code.</summary>
    public string Technology { get; init; } = string.Empty;

    /// <summary>Gets the reserved machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the reserved machine display name.</summary>
    public string? MachineName { get; init; }

    /// <summary>Gets the queue position reserved on the machine.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC.</summary>
    public DateTime ScheduledEndTime { get; init; }

    /// <summary>Gets the setup time reserved in minutes.</summary>
    public int SetupTimeMinutes { get; init; }

    /// <summary>Gets the production run time reserved in minutes.</summary>
    public int ProductionTimeMinutes { get; init; }

    /// <summary>Gets the quoted quantity covered by the hold.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets the lifecycle status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets optional planning notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the user that created the hold.</summary>
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Gets the creation timestamp in UTC.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Gets the last update timestamp in UTC.</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Gets the automatic expiration timestamp in UTC.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>Gets the converted job identifier when available.</summary>
    public Guid? ConvertedJobId { get; init; }

    /// <summary>Maps a planning hold entity to a response DTO.</summary>
    public static ProductionPlanningHoldDto FromEntity(ProductionPlanningHold hold) => new()
    {
        Id = hold.Id,
        ProjectId = hold.ProjectId,
        ProjectPartId = hold.ProjectPartId,
        Technology = hold.Technology,
        MachineId = hold.MachineId,
        MachineName = hold.MachineName,
        QueuePosition = hold.QueuePosition,
        ScheduledStartTime = hold.ScheduledStartTime,
        ScheduledEndTime = hold.ScheduledEndTime,
        SetupTimeMinutes = hold.SetupTimeMinutes,
        ProductionTimeMinutes = hold.ProductionTimeMinutes,
        Quantity = hold.Quantity,
        Status = hold.Status.ToString(),
        Notes = hold.Notes,
        CreatedBy = hold.CreatedBy,
        CreatedAt = hold.CreatedAt,
        UpdatedAt = hold.UpdatedAt,
        ExpiresAt = hold.ExpiresAt,
        ConvertedJobId = hold.ConvertedJobId
    };
}

/// <summary>
/// Request DTO for creating a tentative production planning hold.
/// </summary>
public sealed record CreateProductionPlanningHoldRequest
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

    /// <summary>Gets the requested queue position. Zero appends to the queue.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC, if known.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupTimeMinutes { get; init; }

    /// <summary>Gets the production run time in minutes.</summary>
    public int ProductionTimeMinutes { get; init; }

    /// <summary>Gets the quoted quantity covered by this hold.</summary>
    public int Quantity { get; init; }

    /// <summary>Gets optional planning notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the UTC timestamp when the hold expires.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>Gets a value indicating whether the hold should auto-snap to the next available slot when the requested time conflicts.</summary>
    public bool AutoSnapToNextAvailable { get; init; }

    /// <summary>Maps this request to an application command.</summary>
    public CreatePlanningHoldCommand ToCommand() => new()
    {
        ProjectId = ProjectId,
        ProjectPartId = ProjectPartId,
        Technology = Technology,
        MachineId = MachineId,
        MachineName = MachineName,
        QueuePosition = QueuePosition,
        ScheduledStartTime = ScheduledStartTime,
        ScheduledEndTime = ScheduledEndTime,
        SetupTimeMinutes = SetupTimeMinutes,
        ProductionTimeMinutes = ProductionTimeMinutes,
        Quantity = Quantity,
        Notes = Notes,
        ExpiresAt = ExpiresAt,
        AutoSnap = AutoSnapToNextAvailable,
    };
}

/// <summary>
/// Request DTO for updating a tentative production planning hold.
/// </summary>
public sealed record UpdateProductionPlanningHoldRequest
{
    /// <summary>Gets the target machine identifier or asset code.</summary>
    public string MachineId { get; init; } = string.Empty;

    /// <summary>Gets the target machine display name.</summary>
    public string? MachineName { get; init; }

    /// <summary>Gets the requested queue position. Zero appends to the queue.</summary>
    public int QueuePosition { get; init; }

    /// <summary>Gets the scheduled start time in UTC.</summary>
    public DateTime ScheduledStartTime { get; init; }

    /// <summary>Gets the scheduled end time in UTC, if known.</summary>
    public DateTime? ScheduledEndTime { get; init; }

    /// <summary>Gets the setup time in minutes.</summary>
    public int SetupTimeMinutes { get; init; }

    /// <summary>Gets the production run time in minutes.</summary>
    public int ProductionTimeMinutes { get; init; }

    /// <summary>Gets optional planning notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the UTC timestamp when the hold expires.</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>Maps this request to an application command.</summary>
    public UpdatePlanningHoldCommand ToCommand() => new()
    {
        MachineId = MachineId,
        MachineName = MachineName,
        QueuePosition = QueuePosition,
        ScheduledStartTime = ScheduledStartTime,
        ScheduledEndTime = ScheduledEndTime,
        SetupTimeMinutes = SetupTimeMinutes,
        ProductionTimeMinutes = ProductionTimeMinutes,
        Notes = Notes,
        ExpiresAt = ExpiresAt
    };
}
