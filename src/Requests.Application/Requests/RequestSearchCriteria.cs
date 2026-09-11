using Requests.Domain.Entities;

namespace Requests.Application.Requests;

/// <summary>
/// Validated, normalized search criteria handed to the repository.
/// <para>
/// Only <see cref="RequestService"/> constructs this type, and it is the only place the trusted
/// identity (<see cref="CurrentUserId"/> / <see cref="IsAdministrator"/>) is combined with
/// client-supplied filters. Because these two properties do not exist on
/// <see cref="RequestSearchParameters"/>, no client can set them via model binding.
/// </para>
/// <para>
/// By the time the repository receives this, sort values are typed, paging is within bounds, dates
/// are UTC and the request number is trimmed — so the repository needs no validation of its own.
/// </para>
/// </summary>
public sealed record RequestSearchCriteria
{
    public required int CurrentUserId { get; init; }

    public required bool IsAdministrator { get; init; }

    /// <summary>Trimmed; <c>null</c> when no filter should be applied.</summary>
    public string? RequestNumber { get; init; }

    /// <summary><c>null</c> or empty when no filter should be applied.</summary>
    public IReadOnlyList<RequestStatus>? Statuses { get; init; }

    public RequestType? RequestType { get; init; }

    /// <summary>Inclusive lower bound.</summary>
    public DateTime? CreatedFrom { get; init; }

    /// <summary>
    /// Upper bound, interpreted according to <see cref="CreatedToIsExclusive"/>.
    /// </summary>
    public DateTime? CreatedTo { get; init; }

    /// <summary>
    /// When <c>true</c>, <see cref="CreatedTo"/> is an exclusive bound (<c>&lt;</c>); when
    /// <c>false</c>, it is inclusive (<c>&lt;=</c>).
    /// <para>
    /// The service sets this to <c>true</c> after widening a date-only value to the start of the
    /// following day, which is how "from the 15th to the 15th" covers the whole of the 15th without
    /// relying on a <c>23:59:59.999</c> sentinel whose correctness depends on column precision.
    /// </para>
    /// </summary>
    public bool CreatedToIsExclusive { get; init; }

    public required RequestSortField SortBy { get; init; }

    public required RequestSortDirection SortDirection { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }
}
