namespace Requests.Application.Common;

/// <summary>
/// A single page of results together with the total number of records matching the query.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
