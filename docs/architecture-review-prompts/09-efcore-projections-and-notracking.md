# Prompt 09 — EF Core projections, split queries, and consistent `AsNoTracking`

> Addresses **Top-10 item 9** / Performance & Data-Access Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). Read paths load full entity graphs and
> filter in memory; `AsNoTracking` is applied inconsistently.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution (EF Core + SQL Server). Improve read-query
efficiency and tracking hygiene without changing behaviour. Work incrementally, one query at a time,
measuring before/after where possible.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Data/CardsDbContext.cs` exposes ~27 `DbSet`s; there is **no repository
  layer** (handlers use `CardsDbContext` directly) — this is fine and should be kept.
- GameFlow handlers (`src/CardGames.Poker.Api/GameFlow/*FlowHandler.cs`) and query handlers commonly
  `Include(...)` full graphs and then `Where`/`FirstOrDefault` **in memory**; explicit `.Select(...)`
  projections are rare.
- `AsNoTracking` appears at only ~15 sites, so many read-only queries still pay for change tracking.
- Read/write context conventions are partly captured in `docs/DatabaseCalls.md` and the
  `efcore-architecture` guidance.

### Goal

1. **Reads → `AsNoTracking`.** Audit query handlers and broadcasters
   (`GetGameQueryHandler`, `TableStateBuilder.*`, `LobbyStateBroadcastingBehavior`, GameFlow read
   paths) and apply `AsNoTracking()` to every query whose results are not subsequently mutated and
   saved. Leave tracking on for write/command paths.
2. **Project, don't over-fetch.** For queries that feed DTOs/projections (e.g. table state, lobby
   lists, hand history reads), replace `Include(...)` + in-memory filtering with server-side
   `.Where(...)` and `.Select(...)` into the projection/DTO so SQL returns only needed columns/rows.
3. **Split large includes.** Where a single query fans out into multiple collection `Include`s
   (cartesian explosion risk), add `.AsSplitQuery()`.
4. **Document the convention.** Add a short read-vs-write rule to `docs/DatabaseCalls.md`: reads use
   `AsNoTracking` + projection; commands track and `SaveChanges`.

### Constraints

- Behaviour must be identical — projections must select exactly the data the existing code uses.
- Be careful with owned types/value objects and computed properties when projecting.
- Do not introduce N+1: verify each refactor issues a single round-trip (use the EF Core logging /
  the existing OpenTelemetry EF instrumentation in Development).

### Tests / verification

- Existing integration tests (`Tests/CardGames.IntegrationTests`, real SQL Server) must stay green —
  they are the safety net for these refactors.
- Where helpful, assert query counts using EF Core logging in a focused integration test for a hot
  path (e.g. building table state for a full table).
- Optionally benchmark a hot read with the existing `CardGames.Core.Benchmarks` harness.

### Acceptance criteria

- Hot read paths use `AsNoTracking` + projections; no behavioural changes in tests.
- At least the table-state and lobby read paths are converted and verified single round-trip.
- `docs/DatabaseCalls.md` documents the read/write convention.
- Build + integration tests green from `src`.
