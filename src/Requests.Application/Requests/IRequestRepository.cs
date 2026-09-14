using Requests.Application.Common;

namespace Requests.Application.Requests;

public interface IRequestRepository
{
    /// <summary>
    /// Executes a single query that applies authorization, filtering, sorting and pagination, and
    /// projects straight to <see cref="RequestDto"/>.
    /// </summary>
    Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
