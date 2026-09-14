namespace Requests.Application.Requests;

/// <summary>
/// The fields a client is allowed to sort by. <c>Id</c> is intentionally absent: it is used only as
/// an internal tiebreaker to keep pagination stable, not as a client-selectable option.
/// </summary>
public enum RequestSortField
{
    RequestNumber = 1,
    Status = 2,
    RequestType = 3,
    CreatedAt = 4
}

public enum RequestSortDirection
{
    Ascending = 1,
    Descending = 2
}

/// <summary>
/// Parsing for the client-supplied <c>sortBy</c> / <c>sortDirection</c> values.
/// </summary>
public static class RequestSorting
{
    public const string DefaultSortBy = "createdAt";
    public const string DefaultSortDirection = "desc";

    public static IReadOnlyList<string> SupportedFields { get; } =
        ["requestNumber", "status", "requestType", "createdAt"];

    public static IReadOnlyList<string> SupportedDirections { get; } = ["asc", "desc"];

    public static bool TryParseField(string? value, out RequestSortField field)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "requestnumber":
                field = RequestSortField.RequestNumber;
                return true;
            case "status":
                field = RequestSortField.Status;
                return true;
            case "requesttype":
                field = RequestSortField.RequestType;
                return true;
            case "createdat":
                field = RequestSortField.CreatedAt;
                return true;
            default:
                field = default;
                return false;
        }
    }

    public static bool TryParseDirection(string? value, out RequestSortDirection direction)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "asc":
                direction = RequestSortDirection.Ascending;
                return true;
            case "desc":
                direction = RequestSortDirection.Descending;
                return true;
            default:
                direction = default;
                return false;
        }
    }
}
