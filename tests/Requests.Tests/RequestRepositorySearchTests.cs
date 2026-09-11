using Microsoft.EntityFrameworkCore;
using Requests.Application.Requests;
using Requests.Domain.Entities;
using Requests.Infrastructure.Persistence;
using Requests.Infrastructure.Repositories;
using Xunit;

namespace Requests.Tests;

/// <summary>
/// Exercises the real EF Core query built by <see cref="RequestRepository"/>, since authorization,
/// filtering, sorting and pagination now live in the query rather than in the service.
/// </summary>
public class RequestRepositorySearchTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RegularUser_SeesOnlyOwnedOrAssignedRequests()
    {
        var repository = CreateRepository(
            Create(1, ownerId: 7, assignedTo: 99),   // owned by the caller
            Create(2, ownerId: 99, assignedTo: 7),   // assigned to the caller
            Create(3, ownerId: 99, assignedTo: 98),  // someone else's
            Create(4, ownerId: 99, assignedTo: null) // someone else's, unassigned
        );

        var result = await repository.SearchAsync(Criteria(userId: 7));

        Assert.Equal([1, 2], result.Items.Select(x => x.Id).Order());
        Assert.DoesNotContain(result.Items, x => x.Id is 3 or 4);

        // Proves authorization is applied before CountAsync, not after materialization:
        // an unfiltered count would be 4.
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Administrator_SeesAllRequests()
    {
        var repository = CreateRepository(
            Create(1, ownerId: 7, assignedTo: null),
            Create(2, ownerId: 99, assignedTo: 98),
            Create(3, ownerId: 55, assignedTo: null)
        );

        var result = await repository.SearchAsync(Criteria(userId: 7, isAdministrator: true));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task RequestNumber_PartialMatch_MatchesMiddleOfValue()
    {
        var repository = CreateRepository(
            Create(1, number: "REQ-000123"),
            Create(2, number: "REQ-004560"),
            Create(3, number: "REQ-000999")
        );

        // "0123" is not a prefix of any value, so a StartsWith implementation would return nothing.
        var result = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { RequestNumber = "0123" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("REQ-000123", result.Items.Single().RequestNumber);
    }

    [Fact]
    public async Task Statuses_FiltersBySingleAndMultipleValues()
    {
        var repository = CreateRepository(
            Create(1, status: RequestStatus.New),
            Create(2, status: RequestStatus.InProgress),
            Create(3, status: RequestStatus.Completed)
        );

        var single = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { Statuses = [RequestStatus.New] });

        Assert.Equal(1, single.TotalCount);
        Assert.Equal(RequestStatus.New, single.Items.Single().Status);

        var multiple = await repository.SearchAsync(
            Criteria(isAdministrator: true) with
            {
                Statuses = [RequestStatus.New, RequestStatus.InProgress]
            });

        Assert.Equal(2, multiple.TotalCount);
        Assert.Equal([1, 2], multiple.Items.Select(x => x.Id).Order());
        Assert.DoesNotContain(multiple.Items, x => x.Status == RequestStatus.Completed);
    }

    [Fact]
    public async Task RequestType_Filters()
    {
        var repository = CreateRepository(
            Create(1, type: RequestType.Legal),
            Create(2, type: RequestType.Payment),
            Create(3, type: RequestType.Legal)
        );

        var result = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { RequestType = RequestType.Legal });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal(RequestType.Legal, x.RequestType));
    }

    [Fact]
    public async Task CreatedRange_IsInclusiveOnBothBounds()
    {
        var from = Base.AddDays(10);
        var to = Base.AddDays(20);

        var repository = CreateRepository(
            Create(1, createdAt: from.AddTicks(-1)), // just outside the lower bound
            Create(2, createdAt: from),              // exactly on the lower bound
            Create(3, createdAt: Base.AddDays(15)),  // inside
            Create(4, createdAt: to),                // exactly on the upper bound
            Create(5, createdAt: to.AddTicks(1))     // just outside the upper bound
        );

        var result = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { CreatedFrom = from, CreatedTo = to });

        Assert.Equal([2, 3, 4], result.Items.Select(x => x.Id).Order());
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Sorting_And_Pagination_ProduceStableDisjointPages()
    {
        // Every row shares the same CreatedAt, so ordering is decided entirely by the Id
        // tiebreaker. Without it, pages could overlap or drop rows.
        var tied = Enumerable.Range(1, 5)
            .Select(i => Create(i, createdAt: Base))
            .ToArray();

        var repository = CreateRepository(tied);

        var page1 = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { Page = 1, PageSize = 2 });
        var page2 = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { Page = 2, PageSize = 2 });

        Assert.Equal([1, 2], page1.Items.Select(x => x.Id));
        Assert.Equal([3, 4], page2.Items.Select(x => x.Id));

        // TotalCount describes the whole result set, not the page.
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(2, page1.PageSize);

        // Explicit sorting on a whitelisted field, both directions.
        var ascending = await repository.SearchAsync(
            Criteria(isAdministrator: true) with
            {
                SortBy = RequestSortField.RequestNumber,
                SortDirection = RequestSortDirection.Ascending
            });
        var descending = await repository.SearchAsync(
            Criteria(isAdministrator: true) with
            {
                SortBy = RequestSortField.RequestNumber,
                SortDirection = RequestSortDirection.Descending
            });

        Assert.Equal(
            ascending.Items.Select(x => x.RequestNumber).Reverse(),
            descending.Items.Select(x => x.RequestNumber));
    }

    [Fact]
    public async Task ExtremelyLargePage_ReturnsEmpty_AndNeverWrapsToAnEarlierPage()
    {
        var repository = CreateRepository(
            Enumerable.Range(1, 10).Select(i => Create(i)).ToArray());

        var firstPage = await repository.SearchAsync(
            Criteria(isAdministrator: true) with { Page = 1, PageSize = 100 });

        Assert.Equal(10, firstPage.Items.Count);

        // (page - 1) * pageSize overflows a signed 32-bit int for these values:
        //   (42949674 - 1) * 100 = 4_294_967_300, which wraps to 4
        //   (1000000  - 1) * 100 =    99_999_900, which does not wrap but is far past the end
        // Before the fix the first of these returned real records offset by 4 rows.
        foreach (var page in new[] { 42_949_674, RequestSearchParameters.MaxPage })
        {
            var result = await repository.SearchAsync(
                Criteria(isAdministrator: true) with { Page = page, PageSize = 100 });

            Assert.Empty(result.Items);

            // TotalCount still describes the whole result set, so the client can tell it overshot.
            Assert.Equal(10, result.TotalCount);
        }
    }

    [Fact]
    public async Task SameDay_CreatedFromAndCreatedTo_IncludesThatEntireDay()
    {
        var day = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var repository = CreateRepository(
            Create(1, createdAt: day.AddDays(-1).AddHours(23)), // 14 Jan, 23:00
            Create(2, createdAt: day),                          // 15 Jan, 00:00:00
            Create(3, createdAt: day.AddHours(13).AddMinutes(45)),
            Create(4, createdAt: day.AddHours(23).AddMinutes(59).AddSeconds(59)),
            Create(5, createdAt: day.AddDays(1))                // 16 Jan, 00:00:00
        );

        // What a date picker sends when the user selects 15 January for both ends.
        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new RequestSearchParameters
            {
                CreatedFrom = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified),
                CreatedTo = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Unspecified)
            },
            currentUserId: 1,
            isAdministrator: true);

        Assert.Equal([2, 3, 4], result.Items.Select(x => x.Id).Order());
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task ExplicitTimestamp_CreatedTo_StaysInclusive()
    {
        var day = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var cutoff = day.AddHours(12);

        var repository = CreateRepository(
            Create(1, createdAt: cutoff.AddSeconds(-1)),
            Create(2, createdAt: cutoff),            // exactly on the bound: must be included
            Create(3, createdAt: cutoff.AddSeconds(1))
        );

        var service = new RequestService(repository);
        var result = await service.SearchAsync(
            new RequestSearchParameters { CreatedTo = cutoff },
            currentUserId: 1,
            isAdministrator: true);

        // A non-midnight value is a deliberate instant, so it is not widened to end-of-day.
        Assert.Equal([1, 2], result.Items.Select(x => x.Id).Order());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static RequestRepository CreateRepository(params Request[] requests)
    {
        // A distinct database per test keeps them isolated.
        var options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new RequestsDbContext(options);
        db.Requests.AddRange(requests);
        db.SaveChanges();

        return new RequestRepository(db);
    }

    private static RequestSearchCriteria Criteria(
        int userId = 1,
        bool isAdministrator = false)
        => new()
        {
            CurrentUserId = userId,
            IsAdministrator = isAdministrator,
            SortBy = RequestSortField.CreatedAt,
            SortDirection = RequestSortDirection.Ascending,
            Page = 1,
            PageSize = 20
        };

    private static Request Create(
        int id,
        int ownerId = 1,
        int? assignedTo = null,
        string? number = null,
        RequestStatus status = RequestStatus.New,
        RequestType type = RequestType.General,
        DateTime? createdAt = null)
        => new()
        {
            Id = id,
            RequestNumber = number ?? $"REQ-{id:000000}",
            CustomerId = id,
            OwnerId = ownerId,
            AssignedToUserId = assignedTo,
            Status = status,
            RequestType = type,
            CreatedAt = createdAt ?? Base.AddDays(id),
            UpdatedAt = createdAt ?? Base.AddDays(id)
        };
}
