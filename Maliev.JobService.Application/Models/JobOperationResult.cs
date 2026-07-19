using Maliev.JobService.Domain.Entities;

namespace Maliev.JobService.Application.Models;

/// <summary>
/// Represents the outcome of a job command operation.
/// </summary>
public sealed record JobOperationResult
{
    private JobOperationResult(bool isSuccess, bool isNotFound, Job? job, string? error)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        Job = job;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the target job was not found.
    /// </summary>
    public bool IsNotFound { get; }

    /// <summary>
    /// Gets the updated job when the operation succeeds.
    /// </summary>
    public Job? Job { get; }

    /// <summary>
    /// Gets the operation error message.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    /// <param name="job">The updated job.</param>
    /// <returns>A successful result.</returns>
    public static JobOperationResult Success(Job job) => new(true, false, job, null);

    /// <summary>
    /// Creates a not-found operation result.
    /// </summary>
    /// <returns>A not-found result.</returns>
    public static JobOperationResult NotFound() => new(false, true, null, null);

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failure result.</returns>
    public static JobOperationResult Failure(string error) => new(false, false, null, error);
}
