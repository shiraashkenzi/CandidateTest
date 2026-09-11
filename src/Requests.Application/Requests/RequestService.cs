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

        // The repository applies authorization, filtering, sorting and pagination in the database.
        // Nothing is filtered here: doing so would mean fetching rows only to discard them.
        return await _repository.SearchAsync(criteria, cancellationToken);
    }

    /// <summary>
    /// Validates and normalizes client input, then combines it with the trusted identity.
    /// <para>
    /// The API layer already rejects these cases with a 400 via model validation. These checks are
    /// deliberate defence-in-depth so the service holds its own invariants for any non-HTTP caller,
    /// and they reuse <see cref="RequestSorting"/> rather than repeating the whitelist.
    /// </para>
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

        // Validate the range as the caller expressed it, before the upper bound is widened, so the
        // error reflects what was actually sent.
        if (createdFrom.HasValue && createdTo.HasValue && createdFrom > createdTo)
        {
            throw new ArgumentException(
                "createdFrom must be earlier than or equal to createdTo.", nameof(parameters));
        }

        var (createdToBound, createdToIsExclusive) = NormalizeUpperBound(createdTo);

        var requestNumber = string.IsNullOrWhiteSpace(parameters.RequestNumber)
            ? null
            : parameters.RequestNumber.Trim();

        // Collapse an empty array to null so the repository can skip the predicate entirely.
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
    /// The entity stores UTC (see <c>DbSeeder</c>, which uses <see cref="DateTime.UtcNow"/>), so
    /// values arriving without a kind are treated as UTC rather than as server-local time.
    /// </summary>
    private static DateTime? NormalizeToUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } => value,
        { Kind: DateTimeKind.Local } => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    /// <summary>
    /// Turns the caller's <c>createdTo</c> into the bound the query should actually apply.
    /// <para>
    /// A date picker sends a bare date, which binds to midnight. Comparing <c>&lt;= midnight</c>
    /// would match only the single instant at the start of the day, so selecting the same date for
    /// From and To would return nothing. A midnight value is therefore widened to the start of the
    /// next day and applied exclusively — equivalent to "any time during that day", without
    /// depending on the column's timestamp precision the way a <c>23:59:59.999</c> sentinel would.
    /// </para>
    /// <para>
    /// A value carrying a non-midnight time is a deliberate instant, so it is left untouched and
    /// stays inclusive. A timestamp of exactly midnight is treated as a date-only bound: once model
    /// binding has produced a <see cref="DateTime"/>, "2026-01-15" and "2026-01-15T00:00:00Z" are
    /// indistinguishable, so both widen to cover the whole day.
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
