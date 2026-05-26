using System.ComponentModel.DataAnnotations;
using Maliev.JobService.Application.Models;

namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Request payload for updating editable production details on a job.
/// </summary>
public sealed record UpdateJobDetailsRequest
{
    /// <summary>Gets the customer identifier to attach to the job.</summary>
    [MaxLength(80)]
    public string? CustomerId { get; init; }

    /// <summary>Gets the customer display name captured for shop-floor context.</summary>
    [MaxLength(200)]
    public string? CustomerName { get; init; }

    /// <summary>Gets the material identifier selected for the job.</summary>
    public Guid? MaterialId { get; init; }

    /// <summary>Gets the display name of the assigned operator.</summary>
    [MaxLength(200)]
    public string? AssignedOperator { get; init; }

    /// <summary>Gets the production priority. Lower values are higher priority.</summary>
    [Range(0, 999)]
    public int? Priority { get; init; }

    /// <summary>
    /// Converts the request to an application command.
    /// </summary>
    /// <returns>The application command.</returns>
    public UpdateJobDetailsCommand ToCommand() => new()
    {
        CustomerId = CustomerId,
        CustomerName = CustomerName,
        MaterialId = MaterialId,
        AssignedOperator = AssignedOperator,
        Priority = Priority,
    };
}
