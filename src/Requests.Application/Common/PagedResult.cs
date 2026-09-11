namespace Requests.Application.Common;

/// <summary>
/// A single page of results together with the total number of records matching the query.
/// The total is returned so callers can render pagination controls without fetching every page.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
