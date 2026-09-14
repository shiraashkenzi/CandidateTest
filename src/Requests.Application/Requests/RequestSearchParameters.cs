using System.ComponentModel.DataAnnotations;
using Requests.Domain.Entities;

namespace Requests.Application.Requests;

/// <summary>
/// Client-supplied search input, bound directly from the query string.
/// <para>
/// Deliberately contains <b>no identity fields</b>: the current user id and the administrator flag
/// live on <see cref="RequestSearchCriteria"/>, which only the service can build, so a caller cannot
/// grant itself administrator visibility through the query string.
/// </para>
/// </summary>
public sealed class RequestSearchParameters : IValidatableObject
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Keeps <c>(page - 1) * pageSize</c> far below <see cref="int.MaxValue"/>, so the offset cannot
    /// overflow and wrap around to an earlier page.
    /// </summary>
    public const int MaxPage = 1_000_000;

    /// <summary>Partial, case-sensitivity depends on the database provider's collation.</summary>
    public string? RequestNumber { get; init; }

    /// <summary>
    /// Repeat the query key for multiple values: <c>?statuses=New&amp;statuses=InProgress</c>.
    /// <para>
    /// An array rather than a read-only collection interface: ASP.NET Core's collection binder
    /// cannot instantiate read-only interfaces and would silently bind <c>null</c>.
    /// </para>
    /// </summary>
    public RequestStatus[]? Statuses { get; init; }

    public RequestType? RequestType { get; init; }

    /// <summary>Inclusive lower bound on <see cref="Request.CreatedAt"/>. Interpreted as UTC.</summary>
    public DateTime? CreatedFrom { get; init; }

    /// <summary>
    /// Upper bound on <see cref="Request.CreatedAt"/>. Interpreted as UTC. A date-only value covers
    /// the whole of that day; a value with an explicit time stays an inclusive bound.
    /// </summary>
    public DateTime? CreatedTo { get; init; }

    public string SortBy { get; init; } = RequestSorting.DefaultSortBy;

    public string SortDirection { get; init; } = RequestSorting.DefaultSortDirection;

    [Range(1, MaxPage, ErrorMessage = "page must be between 1 and 1000000.")]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// True for the comma form, e.g. <c>?statuses=1,2</c>. .NET parses that as a bitwise combination
    /// even for a non-<c>[Flags]</c> enum — <c>1,2</c> becomes <c>1 | 2 = 3</c>, a different valid
    /// status — so it is rejected rather than silently returning the wrong rows.
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
