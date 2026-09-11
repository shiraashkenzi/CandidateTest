using Requests.Application.Common;

namespace Requests.Application.Requests;

public interface IRequestService
{
    /// <summary>
    /// Validates and normalizes <paramref name="parameters"/>, combines them with the caller's
    /// identity, and returns a single authorized page of results.
    /// </summary>
    /// <param name="currentUserId">Trusted caller id. Never taken from <paramref name="parameters"/>.</param>
    /// <param name="isAdministrator">Trusted role flag. Administrators bypass the visibility filter.</param>
    /// <exception cref="ArgumentException">Thrown when the parameters are invalid.</exception>
    Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchParameters parameters,
        int currentUserId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
