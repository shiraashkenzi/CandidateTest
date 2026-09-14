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
    /// Applies authorization, filtering, sorting and pagination to a single query and materializes
    /// it only at the end. The queryable never leaves this method.
    /// </summary>
    public async Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Request> query = _db.Requests.AsNoTracking();

        // Authorization is part of the predicate, not a post-filter: unauthorized rows are never
        // fetched and never counted.
        if (!criteria.IsAdministrator)
        {
            var currentUserId = criteria.CurrentUserId;
            query = query.Where(r =>
                r.OwnerId == currentUserId || r.AssignedToUserId == currentUserId);
        }

        // Translates to LIKE '%term%'.
        if (!string.IsNullOrWhiteSpace(criteria.RequestNumber))
        {
            var requestNumber = criteria.RequestNumber;
            query = query.Where(r => r.RequestNumber.Contains(requestNumber));
        }

        // Translates to Status IN (...).
        if (criteria.Statuses is { Count: > 0 } statuses)
        {
            query = query.Where(r => statuses.Contains(r.Status));
        }

        if (criteria.RequestType is { } requestType)
        {
            query = query.Where(r => r.RequestType == requestType);
        }

        if (criteria.CreatedFrom is { } createdFrom)
        {
            query = query.Where(r => r.CreatedAt >= createdFrom);
        }

        if (criteria.CreatedTo is { } createdTo)
        {
            // Exclusive when the service widened a date-only value to the next day.
            query = criteria.CreatedToIsExclusive
                ? query.Where(r => r.CreatedAt < createdTo)
                : query.Where(r => r.CreatedAt <= createdTo);
        }

        // Counted while filtered but unpaged — the only position that yields a correct total.
        var totalCount = await query.CountAsync(cancellationToken);

        // long, because an int multiplication could overflow and wrap to a small or negative offset,
        // silently serving rows from an earlier page.
        var offset = (long)(criteria.Page - 1) * criteria.PageSize;

        if (offset >= totalCount)
        {
            return new PagedResult<RequestDto>([], totalCount, criteria.Page, criteria.PageSize);
        }

        // Ordering must precede Skip/Take or pages are non-deterministic. The cast is safe: offset
        // is below totalCount, which is an int.
        query = ApplySort(query, criteria.SortBy, criteria.SortDirection)
            .Skip((int)offset)
            .Take(criteria.PageSize);

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
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestDto>(items, totalCount, criteria.Page, criteria.PageSize);
    }

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
            // Handled explicitly so a new enum member fails loudly instead of silently sorting by
            // CreatedAt.
            _ => throw new ArgumentOutOfRangeException(
                nameof(field), field, "Unsupported sort field.")
        };

        // Without a tiebreaker, rows sharing a sort value could appear on two pages or on none.
        return ordered.ThenBy(r => r.Id);
    }
}
