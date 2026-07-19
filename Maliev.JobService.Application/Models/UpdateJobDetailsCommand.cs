namespace Maliev.JobService.Application.Models;

/// <summary>
/// Command for updating editable production details on a job.
/// </summary>
public sealed record UpdateJobDetailsCommand
{
    /// <summary>Gets the optional customer identifier to attach to the job.</summary>
    public string? CustomerId { get; init; }

    /// <summary>Gets the optional customer display name to capture on the job.</summary>
    public string? CustomerName { get; init; }

    /// <summary>Gets the optional replacement material identifier.</summary>
    public Guid? MaterialId { get; init; }

    /// <summary>Gets the optional assigned operator display name.</summary>
    public string? AssignedOperator { get; init; }

    /// <summary>Gets the optional production priority. Lower values are higher priority.</summary>
    public int? Priority { get; init; }
}
