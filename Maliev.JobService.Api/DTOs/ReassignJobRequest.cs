using System.ComponentModel.DataAnnotations;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Request to reassign a job to a different machine.
/// </summary>
public record ReassignJobRequest
{
    /// <summary>
    /// Gets the identifier of the new machine to assign the job to.
    /// </summary>
    [Required]
    public required string MachineId { get; init; }
}
