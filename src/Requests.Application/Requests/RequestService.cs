using Requests.Application.Common;

namespace Requests.Application.Requests;

public sealed class RequestService : IRequestService
{
    private readonly IRequestRepository _repository;

    public RequestService(IRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchParameters parameters,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var criteria = BuildCriteria(parameters, currentUserId, isAdministrator);

        return await _repository.SearchAsync(criteria, cancellationToken);
    }

    /// <summary>
    /// Validates and normalizes client input, then combines it with the trusted identity. The API
    /// layer already rejects these cases with a 400; the checks are repeated here so the service
    /// holds its own invariants for non-HTTP callers.
    /// </summary>
    private static RequestSearchCriteria BuildCriteria(
        RequestSearchParameters parameters,
        int currentUserId,
        bool isAdministrator)
    {
        if (currentUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentUserId), currentUserId, "currentUserId must be greater than zero.");
        }

        if (!RequestSorting.TryParseField(parameters.SortBy, out var sortBy))
        {
            throw new ArgumentException(
                $"'{parameters.SortBy}' is not a supported sort field. Supported values: " +
                string.Join(", ", RequestSorting.SupportedFields) + ".",
                nameof(parameters));
        }

        if (!RequestSorting.TryParseDirection(parameters.SortDirection, out var sortDirection))
        {
            throw new ArgumentException(
                $"'{parameters.SortDirection}' is not a supported sort direction. Supported values: " +
                string.Join(", ", RequestSorting.SupportedDirections) + ".",
                nameof(parameters));
        }

        if (parameters.Page < 1 || parameters.Page > RequestSearchParameters.MaxPage)
        {
            throw new ArgumentException(
                $"page must be between 1 and {RequestSearchParameters.MaxPage}.",
                nameof(parameters));
        }

        if (parameters.PageSize < 1 || parameters.PageSize > RequestSearchParameters.MaxPageSize)
        {
            throw new ArgumentException(
                $"pageSize must be between 1 and {RequestSearchParameters.MaxPageSize}.",
                nameof(parameters));
        }

        var createdFrom = NormalizeToUtc(parameters.CreatedFrom);
        var createdTo = NormalizeToUtc(parameters.CreatedTo);

        // Validated before the upper bound is widened, so the error reflects what was actually sent.
        if (createdFrom.HasValue && createdTo.HasValue && createdFrom > createdTo)
        {
            throw new ArgumentException(
                "createdFrom must be earlier than or equal to createdTo.", nameof(parameters));
        }

        var (createdToBound, createdToIsExclusive) = NormalizeUpperBound(createdTo);

        var requestNumber = string.IsNullOrWhiteSpace(parameters.RequestNumber)
            ? null
            : parameters.RequestNumber.Trim();

        var statuses = parameters.Statuses is { Length: > 0 }
            ? parameters.Statuses.Distinct().ToArray()
            : null;

        return new RequestSearchCriteria
        {
            CurrentUserId = currentUserId,
            IsAdministrator = isAdministrator,
            RequestNumber = requestNumber,
            Statuses = statuses,
            RequestType = parameters.RequestType,
            CreatedFrom = createdFrom,
            CreatedTo = createdToBound,
            CreatedToIsExclusive = createdToIsExclusive,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    /// <summary>
    /// The entity stores UTC, so a value arriving without a kind is treated as UTC rather than as
    /// server-local time.
    /// </summary>
    private static DateTime? NormalizeToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } => value,
        { Kind: DateTimeKind.Local } => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    /// <summary>
    /// A date picker sends a bare date, which binds to midnight; comparing <c>&lt;= midnight</c>
    /// would match only the first instant of the day. Midnight is therefore widened to the start of
    /// the next day and applied exclusively, which avoids depending on the column's timestamp
    /// precision the way a <c>23:59:59.999</c> sentinel would. A non-midnight time is a deliberate
    /// instant and stays inclusive.
    /// <para>
    /// Note that "2026-01-15" and "2026-01-15T00:00:00Z" are indistinguishable once model binding
    /// has produced a <see cref="DateTime"/>, so both widen to cover the whole day.
    /// </para>
    /// </summary>
    private static (DateTime? Bound, bool IsExclusive) NormalizeUpperBound(DateTime? createdTo)
    {
        if (createdTo is not { } value)
        {
            return (null, false);
        }

        return value.TimeOfDay == TimeSpan.Zero
            ? (value.AddDays(1), true)
            : (value, false);
    }
}
