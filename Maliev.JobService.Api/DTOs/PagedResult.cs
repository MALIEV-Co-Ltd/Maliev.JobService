namespace Maliev.JobService.Api.DTOs;

/// <summary>
/// Represents a paginated collection of items.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public record PagedResult<T>
{
    /// <summary>The collection of items for the current page.</summary>
    public required List<T> Items { get; init; }
    /// <summary>The total number of items across all pages.</summary>
    public int Total { get; init; }
    /// <summary>The current page number.</summary>
    public int Page { get; init; }
    /// <summary>The number of items per page.</summary>
    public int PageSize { get; init; }
    /// <summary>The total number of pages.</summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Creates a new paged result.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="total">Total count.</param>
    /// <param name="page">Current page.</param>
    /// <param name="pageSize">Page size.</param>
    /// <returns>A new PagedResult instance.</returns>
    public static PagedResult<T> Create(List<T> items, int total, int page, int pageSize)
    {
        return new PagedResult<T>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }
}
