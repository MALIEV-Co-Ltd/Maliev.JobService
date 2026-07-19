using System.ComponentModel.DataAnnotations;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Request to cancel a manufacturing job.
/// </summary>
public record CancelJobRequest
{
    /// <summary>
    /// Gets the reason for cancellation.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public required string Reason { get; init; }
}
