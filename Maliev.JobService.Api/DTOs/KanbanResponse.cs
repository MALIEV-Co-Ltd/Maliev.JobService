namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Represents the state of the manufacturing shop floor across different production stages.
/// </summary>
public record KanbanResponse
{
    /// <summary>Jobs awaiting queue assignment.</summary>
    public List<KanbanJobDto> Pending { get; init; } = [];
    /// <summary>Jobs assigned to a machine but not yet started.</summary>
    public List<KanbanJobDto> Queued { get; init; } = [];
    /// <summary>Jobs currently in production.</summary>
    public List<KanbanJobDto> InProgress { get; init; } = [];
    /// <summary>Jobs in post-production finishing.</summary>
    public List<KanbanJobDto> Finishing { get; init; } = [];
    /// <summary>Jobs whose production work is complete and are waiting for quality review.</summary>
    public List<KanbanJobDto> QualityReviewPending { get; init; } = [];
    /// <summary>Legacy alias for jobs whose production work is complete and are waiting for quality review.</summary>
    public List<KanbanJobDto> Completed { get; init; } = [];
    /// <summary>Jobs that have been cancelled.</summary>
    public List<KanbanJobDto> Cancelled { get; init; } = [];
}
