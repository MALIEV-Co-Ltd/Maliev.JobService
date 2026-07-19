using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// API representation of a durable job status transition audit record.
/// </summary>
public record JobStatusTransitionAuditDto
{
    /// <summary>Gets the audit record identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the job identifier.</summary>
    public required Guid JobId { get; init; }

    /// <summary>Gets the status before the transition.</summary>
    public required string PreviousStatus { get; init; }

    /// <summary>Gets the status after the transition.</summary>
    public required string NewStatus { get; init; }

    /// <summary>Gets the operator or service principal that made the transition.</summary>
    public required string ChangedBy { get; init; }

    /// <summary>Gets the transition timestamp in UTC.</summary>
    public required DateTimeOffset ChangedAtUtc { get; init; }

    /// <summary>Gets optional transition notes.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Maps a domain audit record to the API DTO.
    /// </summary>
    /// <param name="audit">The domain audit record.</param>
    /// <returns>The DTO.</returns>
    public static JobStatusTransitionAuditDto FromEntity(JobStatusTransitionAudit audit) => new()
    {
        Id = audit.Id,
        JobId = audit.JobId,
        PreviousStatus = audit.PreviousStatus.ToString(),
        NewStatus = audit.NewStatus.ToString(),
        ChangedBy = audit.ChangedBy,
        ChangedAtUtc = audit.ChangedAtUtc,
        Notes = audit.Notes,
    };
}
