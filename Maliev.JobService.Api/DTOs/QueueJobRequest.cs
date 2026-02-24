using System.ComponentModel.DataAnnotations;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Request to assign a job to a machine.
/// </summary>
public record QueueJobRequest
{
    /// <summary>
    /// Gets the identifier of the machine to assign the job to.
    /// </summary>
    [Required]
    public required string MachineId { get; init; }
}
