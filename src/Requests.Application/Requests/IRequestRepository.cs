using Requests.Application.Common;

namespace Requests.Application.Requests;

public interface IRequestRepository
{
    /// <summary>
    /// Executes a single database query that applies authorization, filtering, sorting and
    /// pagination, and projects straight to <see cref="RequestDto"/>.
    /// <para>
    /// The contract returns a materialized page rather than an <c>IQueryable</c> on purpose: query
    /// composition — and therefore the decision about what actually reaches the database — stays
    /// inside the repository.
    /// </para>
    /// </summary>
    Task<PagedResult<RequestDto>> SearchAsync(
        RequestSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
