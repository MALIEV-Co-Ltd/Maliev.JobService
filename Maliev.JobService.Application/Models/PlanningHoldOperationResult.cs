using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Application.Models;

/// <summary>
/// Result wrapper for planning hold mutations.
/// </summary>
public sealed class PlanningHoldOperationResult
{
    private PlanningHoldOperationResult(bool isSuccess, bool isNotFound, ProductionPlanningHold? hold, string? error)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        Hold = hold;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the hold was not found.</summary>
    public bool IsNotFound { get; }

    /// <summary>Gets the updated planning hold when available.</summary>
    public ProductionPlanningHold? Hold { get; }

    /// <summary>Gets the failure message when the operation did not succeed.</summary>
    public string? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static PlanningHoldOperationResult Success(ProductionPlanningHold hold) => new(true, false, hold, null);

    /// <summary>Creates a not-found result.</summary>
    public static PlanningHoldOperationResult NotFound() => new(false, true, null, null);

    /// <summary>Creates a failed result.</summary>
    public static PlanningHoldOperationResult Failure(string error) => new(false, false, null, error);
}
