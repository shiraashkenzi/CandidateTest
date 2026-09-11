using Requests.Application.Common;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// The service's own responsibility is validation, normalization and combining client input with the
/// trusted identity. The visibility rule itself is now enforced inside the EF query and is covered by
/// <see cref="RequestRepositorySearchTests"/>.
/// </summary>
public class RequestServiceTests
{
    [Fact]
    public async Task CreatedFrom_After_CreatedTo_IsRejected()
    {
        var repository = new CapturingRequestRepository();
        var service = new RequestService(repository);

        var parameters = new RequestSearchParameters
        {
            CreatedFrom = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedTo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(parameters, currentUserId: 1, isAdministrator: false));

        // The invalid range must never reach the database.
        Assert.Null(repository.LastCriteria);
    }

    [Fact]
    public async Task InvalidSortFieldOrDirection_IsRejected()
    {
        var service = new RequestService(new CapturingRequestRepository());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(
                new RequestSearchParameters { SortBy = "ownerId" },
                currentUserId: 1,
                isAdministrator: false));

        // "id" is a valid internal tiebreaker but is deliberately not client-selectable.
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(
                new RequestSearchParameters { SortBy = "id" },
                currentUserId: 1,
                isAdministrator: false));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SearchAsync(
                new RequestSearchParameters { SortDirection = "sideways" },
                currentUserId: 1,
                isAdministrator: false));
    }

    [Fact]
    public async Task Parameters_AreNormalized_IntoCriteria()
    {
        var repository = new CapturingRequestRepository();
        var service = new RequestService(repository);

        var parameters = new RequestSearchParameters
        {
            RequestNumber = "  REQ-0001  ",
            Statuses = [RequestStatus.New, RequestStatus.New],
            RequestType = RequestType.Legal,
            // No Kind specified: must be interpreted as UTC, matching how the entity stores dates.
            CreatedFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            SortBy = "RequestNumber",
            SortDirection = "ASC",
            Page = 2,
            PageSize = 50
        };

        await service.SearchAsync(parameters, currentUserId: 42, isAdministrator: true);

        var criteria = Assert.IsType<RequestSearchCriteria>(repository.LastCriteria);

        // The trusted identity is what reaches the query — not anything the client could send.
        Assert.Equal(42, criteria.CurrentUserId);
        Assert.True(criteria.IsAdministrator);

        Assert.Equal("REQ-0001", criteria.RequestNumber);
        Assert.Equal([RequestStatus.New], criteria.Statuses);
        Assert.Equal(RequestType.Legal, criteria.RequestType);
        Assert.Equal(DateTimeKind.Utc, criteria.CreatedFrom!.Value.Kind);
        Assert.Equal(RequestSortField.RequestNumber, criteria.SortBy);
        Assert.Equal(RequestSortDirection.Ascending, criteria.SortDirection);
        Assert.Equal(2, criteria.Page);
        Assert.Equal(50, criteria.PageSize);

        // A blank request number is dropped so the repository can skip the predicate entirely.
        await service.SearchAsync(
            new RequestSearchParameters { RequestNumber = "   " },
            currentUserId: 1,
            isAdministrator: false);

        Assert.Null(repository.LastCriteria!.RequestNumber);
    }

    [Fact]
    public void CommaCombinedQueryValues_AreDetected()
    {
        // .NET parses a comma-separated enum value as a bitwise combination, so "1,2" would bind to
        // 1 | 2 = 3 (Completed) and silently return the wrong rows. The API rejects the comma form;
        // multiple values must be sent as repeated query keys.
        Assert.True(RequestSearchParameters.IsCommaCombinedValue("1,2"));
        Assert.True(RequestSearchParameters.IsCommaCombinedValue("New,InProgress"));
        Assert.True(RequestSearchParameters.IsCommaCombinedValue("3,4"));

        // Single values — the supported form — must not be rejected.
        Assert.False(RequestSearchParameters.IsCommaCombinedValue("1"));
        Assert.False(RequestSearchParameters.IsCommaCombinedValue("New"));
        Assert.False(RequestSearchParameters.IsCommaCombinedValue(""));
        Assert.False(RequestSearchParameters.IsCommaCombinedValue(null));
    }

    private sealed class CapturingRequestRepository : IRequestRepository
    {
        public RequestSearchCriteria? LastCriteria { get; private set; }

        public Task<PagedResult<RequestDto>> SearchAsync(
            RequestSearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            LastCriteria = criteria;
            return Task.FromResult(
                new PagedResult<RequestDto>([], 0, criteria.Page, criteria.PageSize));
        }
    }
}
