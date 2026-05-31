# Prompt 06 — Standardize `OneOf` → `IResult` mapping across endpoints

> Addresses **Top-10 item 6** / Architectural & Code Quality Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). Each slice hand-writes its own
> `error.Code switch { … }` mapping, and `GameStateBroadcastingBehavior` detects success via
> reflection over `OneOf` internals.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution (Minimal APIs + MediatR + `OneOf`). Introduce a
single, shared convention for turning command/query results into HTTP responses, and remove the
reflection-based success detection. Make incremental, behaviour-preserving changes.

### Context (verify before editing)

- 240 files use `OneOf`. Handlers return `OneOf<TSuccess, TError>`; endpoints call
  `result.Match(success => Results.Ok(...), error => error.Code switch { ... => Results.* })`. The
  switch blocks are duplicated and drift between slices (e.g. `Features/Leagues/.../CreateLeague
  Endpoint.cs`, `Features/Profile/.../UpdateFavoriteVariantsEndpoint.cs`,
  `Features/Games/Generic/.../PerformShowdownEndpoint.cs`).
- Error types expose an error **code enum** (e.g. `PerformShowdownErrorCode`,
  `CreateLeagueErrorCode`) plus a `Message`.
- `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs` detects
  the success branch by reading the `OneOf` `Index` property via reflection and string-matching
  `OneOf.OneOf` on the generic type definition.

### Goal

1. Define a small shared **error contract**, e.g. an `interface IDomainError { ErrorKind Kind; string
   Message; }` where `ErrorKind` is a common enum (`NotFound`, `Validation`, `Conflict`,
   `Unauthorized`, `Forbidden`, `Unexpected`). Have the existing per-slice error records implement it
   (map their code enum → `ErrorKind`), without deleting the slice-specific codes.
2. Add a single mapping helper, e.g. `ResultMapping.ToHttpResult(error)` →
   `Results.Problem/NotFound/Conflict/...` producing consistent `ProblemDetails`. Replace the
   duplicated `switch` blocks in endpoints with one call. Migrate a few representative slices first,
   then the rest.
3. Replace the reflection in `GameStateBroadcastingBehavior` with an explicit success signal — either
   the existing `IGameStateBroadcastResult.ShouldBroadcastGameState`, or a small
   `IResultEnvelope { bool IsSuccess }` the responses implement — so success detection no longer
   depends on `OneOf` internals.

### Constraints

- **Preserve existing HTTP status codes** for each error code unless the current mapping is clearly
  wrong; this is a consolidation, not a contract change. Capture the current mapping per slice before
  refactoring.
- Do not break OpenAPI/Scalar metadata (`.Produces<...>()` / `.WithName()` stay accurate).
- Migrate incrementally; keep the build green between slices.

### Tests / verification

- Add tests for `ResultMapping.ToHttpResult` covering every `ErrorKind`.
- Run the integration suite to confirm endpoint status codes are unchanged
  (`Tests/CardGames.IntegrationTests`).

### Acceptance criteria

- Endpoints map errors through the shared helper; no per-slice duplicated error switches remain (or a
  clear majority migrated, with a tracking note for the rest).
- `GameStateBroadcastingBehavior` no longer reflects over `OneOf`.
- Build + integration tests green from `src`.
