namespace Maliev.JobService.Application.Models;

/// <summary>
/// Represents a generic paged query result.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>
    /// Gets or sets the current page items.
    /// </summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>
    /// Gets or sets the total number of matching items.
    /// </summary>
    public required int Total { get; init; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    public required int Page { get; init; }

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Gets or sets the total page count.
    /// </summary>
    public required int TotalPages { get; init; }
}
