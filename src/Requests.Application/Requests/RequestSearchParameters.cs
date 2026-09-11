using System.ComponentModel.DataAnnotations;
using Requests.Domain.Entities;

namespace Requests.Application.Requests;

/// <summary>
/// Client-supplied search input, bound directly from the query string by the API layer.
/// <para>
/// This type deliberately contains <b>no identity fields</b>. The current user id and the
/// administrator flag live on <see cref="RequestSearchCriteria"/>, which only the service can build,
/// so a caller cannot grant itself administrator visibility through the query string.
/// </para>
/// <para>
/// Validation attributes live here (rather than on a separate API model) so there is a single input
/// model. <see cref="IValidatableObject"/> and the DataAnnotations attributes are BCL types, so this
/// adds no dependency on ASP.NET Core.
/// </para>
/// </summary>
public sealed class RequestSearchParameters : IValidatableObject
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Upper bound on <see cref="Page"/>. Bounding it keeps <c>(page - 1) * pageSize</c> far below
    /// <see cref="int.MaxValue"/>, so the offset can never overflow and wrap around to an earlier
    /// page. Deep paging beyond this point is not a supported access pattern anyway.
    /// </summary>
    public const int MaxPage = 1_000_000;

    /// <summary>Partial, case-sensitivity depends on the database provider's collation.</summary>
    public string? RequestNumber { get; init; }

    /// <summary>
    /// Repeat the query key for multiple values: <c>?statuses=New&amp;statuses=InProgress</c>.
    /// <para>
    /// Typed as an array rather than a read-only collection interface on purpose: ASP.NET Core's
    /// collection model binder cannot instantiate read-only interfaces, and would silently bind
    /// <c>null</c> instead of the supplied values.
    /// </para>
    /// </summary>
    public RequestStatus[]? Statuses { get; init; }

    public RequestType? RequestType { get; init; }

    /// <summary>Inclusive lower bound on <see cref="Request.CreatedAt"/>. Interpreted as UTC.</summary>
    public DateTime? CreatedFrom { get; init; }

    /// <summary>
    /// Upper bound on <see cref="Request.CreatedAt"/>. Interpreted as UTC.
    /// <para>
    /// A date-only value (midnight) covers the <b>whole of that day</b>: <c>createdTo=2026-01-15</c>
    /// matches everything created on 15 January, because it is normalized to the exclusive bound
    /// <c>CreatedAt &lt; 2026-01-16T00:00:00Z</c>. An exclusive next-day boundary is used rather than
    /// an inclusive <c>23:59:59.999</c> so the result does not depend on the column's timestamp
    /// precision.
    /// </para>
    /// <para>
    /// A value with an explicit non-midnight time is left alone and stays an <b>inclusive</b> bound
    /// (<c>CreatedAt &lt;= value</c>), so a caller asking for a precise instant gets exactly that.
    /// </para>
    /// </summary>
    public DateTime? CreatedTo { get; init; }

    public string SortBy { get; init; } = RequestSorting.DefaultSortBy;

    public string SortDirection { get; init; } = RequestSorting.DefaultSortDirection;

    [Range(1, MaxPage, ErrorMessage = "page must be between 1 and 1000000.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// True when a raw query value uses the comma form, e.g. <c>?statuses=1,2</c>.
    /// <para>
    /// .NET parses a comma-separated enum value as a <i>bitwise combination</i>, even for an enum
    /// that is not <c>[Flags]</c>: <c>1,2</c> becomes <c>1 | 2 = 3</c>, which is a different, valid
    /// status. Left unchecked the request would succeed and silently return the wrong rows, so the
    /// API rejects the comma form instead. Multiple values must be sent as repeated keys.
    /// </para>
    /// </summary>
    public static bool IsCommaCombinedValue(string? rawQueryValue) =>
        rawQueryValue is not null && rawQueryValue.Contains(',');

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!RequestSorting.TryParseField(SortBy, out _))
        {
            yield return new ValidationResult(
                $"'{SortBy}' is not a supported sort field. Supported values: " +
                string.Join(", ", RequestSorting.SupportedFields) + ".",
                [nameof(SortBy)]);
        }

        if (!RequestSorting.TryParseDirection(SortDirection, out _))
        {
            yield return new ValidationResult(
                $"'{SortDirection}' is not a supported sort direction. Supported values: " +
                string.Join(", ", RequestSorting.SupportedDirections) + ".",
                [nameof(SortDirection)]);
        }

        if (CreatedFrom.HasValue && CreatedTo.HasValue && CreatedFrom > CreatedTo)
        {
            yield return new ValidationResult(
                "createdFrom must be earlier than or equal to createdTo.",
                [nameof(CreatedFrom), nameof(CreatedTo)]);
        }
    }
}
