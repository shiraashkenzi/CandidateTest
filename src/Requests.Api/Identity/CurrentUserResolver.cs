namespace Requests.Api.Identity;

/// <summary>The caller's identity for this request.</summary>
public readonly record struct CurrentUser(int UserId, bool IsAdministrator);

/// <summary>
/// Resolves the exercise's header-based identity.
/// <para>
/// IMPORTANT: <c>X-User-Id</c> / <c>X-Is-Admin</c> are an exercise stand-in and are entirely
/// client-controlled — any caller can send <c>X-Is-Admin: true</c>. In a production system the user
/// id and role must come from authenticated claims on <c>HttpContext.User</c>, established by a real
/// authentication scheme. Only this resolver would need to change: the authorization *rule* is
/// already enforced server-side inside the database query, not here and not in the client.
/// </para>
/// <para>
/// Unlike the previous implementation, resolution fails closed. A missing, malformed or non-positive
/// user id is rejected rather than silently defaulting to user 1.
/// </para>
/// </summary>
public static class CurrentUserResolver
{
    public const string UserIdHeader = "X-User-Id";
    public const string IsAdminHeader = "X-Is-Admin";

    /// <summary>
    /// Attempts to resolve the caller. On failure, <paramref name="error"/> describes the problem and
    /// <paramref name="isMissing"/> distinguishes "no identity asserted" (401) from "identity
    /// asserted but invalid" (400).
    /// </summary>
    public static bool TryResolve(
        HttpRequest request,
        out CurrentUser user,
        out string error,
        out bool isMissing)
    {
        user = default;

        var rawUserId = request.Headers[UserIdHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rawUserId))
        {
            error = $"The {UserIdHeader} header is required.";
            isMissing = true;
            return false;
        }

        isMissing = false;

        if (!int.TryParse(rawUserId, out var userId))
        {
            error = $"The {UserIdHeader} header must be an integer.";
            return false;
        }

        if (userId <= 0)
        {
            error = $"The {UserIdHeader} header must be greater than zero.";
            return false;
        }

        var isAdministrator = string.Equals(
            request.Headers[IsAdminHeader].FirstOrDefault(),
            "true",
            StringComparison.OrdinalIgnoreCase);

        user = new CurrentUser(userId, isAdministrator);
        error = string.Empty;
        return true;
    }
}
