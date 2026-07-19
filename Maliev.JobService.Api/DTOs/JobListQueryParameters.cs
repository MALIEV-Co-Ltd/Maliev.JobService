using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Parameters for querying the job list.
/// </summary>
public record JobListQueryParameters
{
    /// <summary>Filter by job status.</summary>
    public JobStatus? Status { get; init; }
    /// <summary>Filter by manufacturing technology.</summary>
    public string? Technology { get; init; }
    /// <summary>Filter by assigned machine.</summary>
    public string? AssignedMachineId { get; init; }
    /// <summary>The page number to retrieve.</summary>
    public int Page { get; init; } = 1;
    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; } = 20;
}
