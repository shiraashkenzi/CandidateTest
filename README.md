# Requests

An ASP.NET Core Web API with server-side search, filtering, sorting and pagination, plus an Angular
frontend for searching and displaying requests. Authorization is enforced on the server as part of
the data query, never by the client. Covers Part A (implementation) and Part B (architecture).

## Tech Stack

**Backend** — .NET 8, ASP.NET Core Web API, EF Core 8 (InMemory provider), Swagger, xUnit. The stack
was kept as provided: the exercise supplies it, the task is to extend it, and it already suits a
query-heavy read endpoint.

**Frontend** — Angular 20, TypeScript, standalone components, Reactive Forms, Signals, plain CSS; no
additional npm packages. Angular was chosen over React because its first-party Reactive Forms and
typed `HttpClient` cover a filter-heavy screen without extra dependencies, and Signals handle the
page state without a state-management library.

## Run

Backend, from the repository root:

```bash
dotnet run --project src/Requests.Api/Requests.Api.csproj
```

Runs on `http://localhost:60702`, with Swagger at `/swagger`. Sample data is seeded at startup.

Frontend, in a second terminal:

```bash
cd frontend
npm install
npm start
```

Runs on `http://localhost:4200`. The dev proxy forwards `/api` to the backend, so start the backend
first; no CORS configuration is needed.

## Tests

```bash
dotnet test tests/Requests.Tests/Requests.Tests.csproj
cd frontend && npm test -- --watch=false --browsers=ChromeHeadless
```

## Part A

- Search, filtering, sorting and pagination are composed into a single query and executed by the
  data provider, rather than applied in memory after loading everything.
- Authorization is part of that same query, so records the user may not see are never materialised,
  and the returned total count reflects only visible records.
- Invalid input returns `400` with `ValidationProblemDetails`; a missing identity header returns
  `401`.
- Angular UI with a filter form, sortable results, pagination, and explicit loading, error and
  no-results states.

## Key Decisions

**Technical decision with alternatives — the repository's query surface.** The repository exposes
`SearchAsync` and returns a materialised paged result. The alternatives were exposing `IQueryable`
outside the repository, or introducing a Specification/CQRS-style abstraction. Returning a page keeps
query composition and execution inside one boundary — no caller can accidentally build an unpaged or
unauthorized query, and there is no leak of persistence concerns into the application layer — while
avoiding an abstraction that a single search use case does not justify.

- **Authorization is applied inside the EF Core query, before materialisation.** The visibility rule
  is composed into the query instead of filtering an already-loaded list, so it also governs the
  total count used for pagination.
- **Identity fails closed.** A missing identity header returns `401`; malformed or non-positive
  returns `400`. There is no fallback to a default user.
- **The frontend separates draft state from applied state.** Filters and identity take effect only
  on Search or Clear, so paging always uses what was actually submitted.

## Assumptions / Limitations

- **EF Core InMemory is used for the exercise.** Data resets on every start, and the provider does
  not offer real relational or transactional behaviour. Queries are written so they translate
  cleanly on a relational provider.
- **`X-User-Id` / `X-Is-Admin` are exercise-only identity headers, not authentication.** Any client
  can claim any identity. The authorization rule itself is still enforced server-side; in production
  the identity would come from authenticated claims.
- **The API is read-only** — create and update operations are outside Part A's scope.

## What Was Not Completed / Next Steps

All assignment requirements are complete, including Part B, which is a design task and is delivered
as a document. A production version would continue with:

- replacing EF Core InMemory with a relational database plus migrations;
- real authentication (claims-based) instead of the exercise-only identity headers;
- implementing the proposed Outbox / broker / Notification Service architecture, which Part B
  specifies as a design only;
- optional controller-level integration tests for the 401/400 identity paths.

## Part B

[docs/architecture.md](docs/architecture.md) describes how the system would evolve as it grows: a
Request Service and a Notification Service communicating asynchronously through a message broker,
using the Transactional Outbox pattern with at-least-once delivery and an idempotent consumer.
