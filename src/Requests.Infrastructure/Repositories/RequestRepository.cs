using Microsoft.EntityFrameworkCore;
using Requests.Application.Common;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Infrastructure.Persistence;

namespace Requests.Infrastructure.Repositories;

public sealed class RequestRepository : IRequestRepository
{
    private readonly RequestsDbContext _db;

    public RequestRepository(RequestsDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Builds one composed <see cref="IQueryable{T}"/> and materializes it only at the end, so that
    /// authorization, filtering, sorting and pagination all execute in the database. The queryable
    /// never leaves this method.
    /// </summary>
    public async Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        // Read-only query: no change tracking, no identity-map entries.
        IQueryable<Request> query = _db.Requests.AsNoTracking();

        // 1. Authorization — part of the query predicate, never a post-filter.
        //    Unauthorized rows are excluded by the database and are never fetched or counted.
        if (!criteria.IsAdministrator)
        {
            var currentUserId = criteria.CurrentUserId;
            query = query.Where(r =>
                r.OwnerId == currentUserId || r.AssignedToUserId == currentUserId);
        }

        // 2. Partial request number match. Translates to LIKE '%term%'.
        if (!string.IsNullOrWhiteSpace(criteria.RequestNumber))
        {
            var requestNumber = criteria.RequestNumber;
            query = query.Where(r => r.RequestNumber.Contains(requestNumber));
        }

        // 3. Status filter. Translates to Status IN (...) for one or many values.
        if (criteria.Statuses is { Count: > 0 } statuses)
        {
            query = query.Where(r => statuses.Contains(r.Status));
        }

        // 4. Request type filter.
        if (criteria.RequestType is { } requestType)
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        // 5. Creation date range, inclusive on both bounds.
        if (criteria.CreatedFrom is { } createdFrom)
        {
            query = query.Where(r => r.CreatedAt >= createdFrom);
        }

        if (criteria.CreatedTo is { } createdTo)
        {
            // Exclusive when the service widened a date-only value to the next day; inclusive when
            // the caller supplied an explicit instant. See RequestSearchCriteria.CreatedToIsExclusive.
            query = criteria.CreatedToIsExclusive
                ? query.Where(r => r.CreatedAt < createdTo)
                : query.Where(r => r.CreatedAt <= createdTo);
        }

        // 6. Count over the filtered-but-unpaged query: the only position that yields a correct
        //    total. Still no rows materialized.
        var totalCount = await query.CountAsync(cancellationToken);

        // Computed as long: an int multiplication here could overflow and wrap to a small or
        // negative offset, which would silently serve rows from an earlier page (and emit a negative
        // SQL OFFSET on a relational provider). Page is also bounded by
        // RequestSearchParameters.MaxPage, so this is belt and braces.
        var offset = (long)(criteria.Page - 1) * criteria.PageSize;

        if (offset >= totalCount)
        {
            // Nothing matched, or the requested page lies past the end of the result set.
            // Either way there is no second round trip to make.
            return new PagedResult<RequestDto>([], totalCount, criteria.Page, criteria.PageSize);
        }

        // 7. Sorting, then 8. pagination — ordering must precede Skip/Take or pages are
        //    non-deterministic. The cast is safe: offset is below totalCount, which is an int.
        query = ApplySort(query, criteria.SortBy, criteria.SortDirection)
            .Skip((int)offset)
            .Take(criteria.PageSize);

        // 9. Projection inside the query: the provider selects only the DTO's columns.
        var items = await query
            .Select(r => new RequestDto(
                r.Id,
                r.RequestNumber,
                r.CustomerId,
                r.OwnerId,
                r.AssignedToUserId,
                r.Status,
                r.RequestType,
                r.CreatedAt))
            .ToListAsync(cancellationToken); // <- the only materialization of rows

        return new PagedResult<RequestDto>(items, totalCount, criteria.Page, criteria.PageSize);
    }

    /// <summary>
    /// Explicit switch over the whitelisted sort fields — no reflection-based dynamic ordering.
    /// </summary>
    private static IQueryable<Request> ApplySort(
        IQueryable<Request> query,
        RequestSortField field,
        RequestSortDirection direction)
    {
        var ascending = direction == RequestSortDirection.Ascending;

        IOrderedQueryable<Request> ordered = field switch
        {
            RequestSortField.RequestNumber => ascending
                ? query.OrderBy(r => r.RequestNumber)
                : query.OrderByDescending(r => r.RequestNumber),
            RequestSortField.Status => ascending
                ? query.OrderBy(r => r.Status)
                : query.OrderByDescending(r => r.Status),
            RequestSortField.RequestType => ascending
                ? query.OrderBy(r => r.RequestType)
                : query.OrderByDescending(r => r.RequestType),
            RequestSortField.CreatedAt => ascending
                ? query.OrderBy(r => r.CreatedAt)
                : query.OrderByDescending(r => r.CreatedAt),
            // Every whitelisted field is handled explicitly, so a new enum member fails loudly here
            // instead of silently falling back to sorting by CreatedAt.
            _ => throw new ArgumentOutOfRangeException(
                nameof(field), field, "Unsupported sort field.")
        };

        // Deterministic tiebreaker. Without it, rows sharing the same sort value could appear on
        // two pages (or on none) because the database is free to order ties arbitrarily.
        return ordered.ThenBy(r => r.Id);
    }
}
