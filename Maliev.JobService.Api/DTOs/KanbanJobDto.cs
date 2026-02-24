using Maliev.JobService.Data.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Simplified job data for Kanban board display.
/// </summary>
public record KanbanJobDto
{
    /// <summary>Gets the unique identifier of the job.</summary>
    public required Guid JobId { get; init; }
    /// <summary>Gets the unique identifier of the associated order.</summary>
    public required Guid OrderId { get; init; }
    /// <summary>Gets the manufacturing technology.</summary>
    public required string Technology { get; init; }
    /// <summary>Gets the unique identifier of the material.</summary>
    public required Guid MaterialId { get; init; }
    /// <summary>Gets the identifier of the assigned machine.</summary>
    public string? AssignedMachineId { get; init; }
    /// <summary>Gets the production priority.</summary>
    public int Priority { get; init; }
    /// <summary>Gets the estimated print time in minutes.</summary>
    public int EstimatedPrintTimeMinutes { get; init; }
    /// <summary>Gets the timestamp when production started.</summary>
    public DateTime? StartedAt { get; init; }
    /// <summary>Gets the timestamp when production was completed.</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Maps a Job entity to a KanbanJobDto.
    /// </summary>
    /// <param name="job">The job entity.</param>
    /// <returns>A new KanbanJobDto instance.</returns>
    public static KanbanJobDto FromEntity(Job job) => new()
    {
        JobId = job.Id,
        OrderId = job.OrderId,
        Technology = job.Technology,
        MaterialId = job.MaterialId,
        AssignedMachineId = job.AssignedMachineId,
        Priority = job.Priority,
        EstimatedPrintTimeMinutes = job.EstimatedPrintTimeMinutes,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt
    };
}
