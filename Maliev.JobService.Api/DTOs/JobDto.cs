using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Data transfer object for a manufacturing job.
/// </summary>
public record JobDto
{
    /// <summary>Gets the unique identifier of the job.</summary>
    public required Guid JobId { get; init; }
    /// <summary>Gets the unique identifier of the associated order.</summary>
    public required Guid OrderId { get; init; }
    /// <summary>Gets the unique identifier of the associated order item.</summary>
    public required Guid OrderItemId { get; init; }
    /// <summary>Gets the originating project ID when this job came from a project quotation.</summary>
    public Guid? SourceProjectId { get; init; }
    /// <summary>Gets the originating project part ID when this job came from a project quotation.</summary>
    public Guid? SourceProjectPartId { get; init; }
    /// <summary>Gets the unique identifier of the material to be used.</summary>
    public required Guid MaterialId { get; init; }
    /// <summary>Gets the manufacturing technology (e.g., FDM, SLA).</summary>
    public required string Technology { get; init; }
    /// <summary>Gets the volume of the part in cubic centimeters.</summary>
    public decimal VolumeCm3 { get; init; }
    /// <summary>Gets the estimated time to print/machine the part in minutes.</summary>
    public int EstimatedPrintTimeMinutes { get; init; }
    /// <summary>Gets the identifier of the machine assigned to this job.</summary>
    public string? AssignedMachineId { get; init; }
    /// <summary>Gets the production priority (lower values indicate higher priority).</summary>
    public int Priority { get; init; }
    /// <summary>Gets the current status of the job.</summary>
    public required string Status { get; init; }
    /// <summary>Gets optional notes or instructions for the job.</summary>
    public string? Notes { get; init; }
    /// <summary>Gets the timestamp when production started.</summary>
    public DateTime? StartedAt { get; init; }
    /// <summary>Gets the timestamp when production was completed.</summary>
    public DateTime? CompletedAt { get; init; }
    /// <summary>Gets the timestamp when the job record was created.</summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>Gets the timestamp when the job record was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
    /// <summary>Employee-only. True if job is outsourced.</summary>
    public bool IsOutsourced { get; init; }
    /// <summary>Gets the scheduled start time for this job.</summary>
    public DateTime? ScheduledStartTime { get; init; }
    /// <summary>Gets the scheduled end time for this job.</summary>
    public DateTime? ScheduledEndTime { get; init; }
    /// <summary>Gets the setup time in minutes required before production.</summary>
    public int SetupTimeMinutes { get; init; }
    /// <summary>Gets the position of this job in the machine queue.</summary>
    public int QueuePosition { get; init; }

    /// <summary>
    /// Maps a Job entity to a JobDto.
    /// </summary>
    /// <param name="job">The job entity.</param>
    /// <returns>A new JobDto instance.</returns>
    public static JobDto FromEntity(Job job) => new()
    {
        JobId = job.Id,
        OrderId = job.OrderId,
        OrderItemId = job.OrderItemId,
        SourceProjectId = job.SourceProjectId,
        SourceProjectPartId = job.SourceProjectPartId,
        MaterialId = job.MaterialId,
        Technology = job.Technology,
        VolumeCm3 = job.VolumeCm3,
        EstimatedPrintTimeMinutes = job.EstimatedPrintTimeMinutes,
        AssignedMachineId = job.AssignedMachineId,
        Priority = job.Priority,
        Status = job.Status.ToString(),
        Notes = job.Notes,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        CreatedAt = job.CreatedAt,
        UpdatedAt = job.UpdatedAt,
        IsOutsourced = job.IsOutsourced,
        ScheduledStartTime = job.ScheduledStartTime,
        ScheduledEndTime = job.ScheduledEndTime,
        SetupTimeMinutes = job.SetupTimeMinutes,
        QueuePosition = job.QueuePosition,
    };
}
