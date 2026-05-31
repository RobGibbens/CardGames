# Architecture & Code Review — CardGames Poker Platform

**Date:** 2026-05-31
**Reviewer role:** Principal Software Architect / Senior .NET Developer
**Stack:** .NET 10 · ASP.NET Core Minimal APIs · Vertical Slice Architecture · MediatR ·
EF Core (SQL Server) · Blazor Server · .NET Aspire · SignalR · xUnit
**Scope:** Whole solution (`src/CardGames.slnx`) — API, Blazor Web, domain libraries, data,
infrastructure, tests.

> This document is **analysis only**. It does **not** modify application code and does **not**
> open implementation pull requests. Every finding is grounded in a concrete repository location.
> Ready-to-paste implementation prompts for the highest-value items live in
> [`docs/architecture-review-prompts/`](architecture-review-prompts/README.md).
>
> Two existing audits already cover their areas in depth and are **not** re-derived here:
> [`docs/SecurityReview.md`](SecurityReview.md) (security) and
> [`docs/ObservabilityAudit.md`](ObservabilityAudit.md) (telemetry). This review references them
> where relevant and focuses on architecture, slice design, code quality, and maintainability.

---

## Executive Summary

CardGames is a mature, feature-rich real-money-style poker platform with a genuinely strong
backbone: a consistent Vertical Slice Architecture under
`src/CardGames.Poker.Api/Features`, MediatR with a well-ordered cross-cutting pipeline,
`OneOf<TSuccess, TError>` discriminated-union results (240 files use `OneOf`), attribute-based
endpoint discovery, API versioning, .NET Aspire orchestration, and an integration test suite that
spins up real SQL Server via Testcontainers. The conventions are documented
(`docs/ARCHITECTURE.md`, `docs/GameVariantBoundary.md`, `docs/WebRouterDesign.md`,
`docs/TableRefreshPolicy.md`) and enforced by guard tests (e.g.
`GameVariantBoundaryTests`). This is well above the median for a project of this size.

The principal risks are **concentration of logic in a handful of very large units** and
**duplication across the per-variant game slices**. A small number of files carry a
disproportionate share of the system's complexity:

| Unit | Lines | Concern |
|------|------:|---------|
| `Components/Pages/TablePlay.razor` | 7,724 | Single Blazor component owns the entire table UI, SignalR wiring, and gameplay logic |
| `Services/TableStateBuilder.Showdown.cs` | 1,576 | Showdown projection for every variant in one partial |
| `Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` | 1,567 | Showdown orchestration + pot award + persistence in one handler |
| `Services/ContinuousPlayBackgroundService.NextHand.cs` | 1,082 | Next-hand progression for all variants |
| `Components/Pages/LeagueDetail.razor` | 2,854 | Large multi-tab page component |

These are maintainability and testability bottlenecks, not correctness defects — the system works
and is well tested — but they are where future change cost and onboarding friction concentrate.

A few small but real **configuration/security hygiene** issues are also worth fixing quickly: a
MediatR license key is committed to source, the default cache duration is effectively disabled
(`2ms`, behind a `TODO:ROB`), and CORS is wide open with credentials enabled.

### Ratings (1–10)

| Dimension | Rating | Rationale |
|-----------|:------:|-----------|
| **Architecture** | 8 | Disciplined vertical slices, clean pipeline, documented and test-enforced boundaries. Loses points for a few god-objects and per-variant duplication. |
| **Maintainability** | 6 | Excellent conventions undercut by 1,000–7,700-line hotspots and ~15× duplicated per-variant handlers. |
| **Scalability** | 7 | Aspire + Redis/FusionCache + SignalR backplane-ready; Blazor Server circuits and the in-process continuous-play loop are the main horizontal-scaling constraints. |
| **Testability** | 7 | Strong integration coverage with real SQL Server; gaps in Blazor component tests (no bUnit) and browser E2E (no Playwright), and the largest units are hard to unit-test in isolation. |

---

## Top 10 Highest-Value Improvements

Each item links to a self-contained implementation prompt in
[`docs/architecture-review-prompts/`](architecture-review-prompts/README.md).

| # | Title | Impact | Effort |
|---|-------|:------:|:------:|
| 1 | [Decompose `TablePlay.razor` into child components + a table view-model service](architecture-review-prompts/01-decompose-tableplay-component.md) | High | High |
| 2 | [Extract showdown pot-award & evaluation services out of the 1,567-line handler](architecture-review-prompts/02-extract-showdown-services.md) | High | High |
| 3 | [Remove the committed MediatR license key from source control](architecture-review-prompts/03-remove-committed-mediatr-license-key.md) | High | Low |
| 4 | [Fix the FusionCache default duration (`2ms` `TODO:ROB`)](architecture-review-prompts/04-fix-fusioncache-default-duration.md) | High | Low |
| 5 | [Tighten the wide-open SignalR CORS policy](architecture-review-prompts/05-tighten-cors-policy.md) | High | Low |
| 6 | [Standardize `OneOf` → `IResult` mapping across endpoints](architecture-review-prompts/06-standardize-result-mapping.md) | Medium | Medium |
| 7 | [Pick one validation strategy; remove the unused FluentValidation wiring](architecture-review-prompts/07-consolidate-validation-strategy.md) | Medium | Low |
| 8 | [Collapse duplicated per-variant Deal/Betting slices behind shared handlers](architecture-review-prompts/08-reduce-per-variant-slice-duplication.md) | High | High |
| 9 | [Add EF Core projections, split queries, and consistent `AsNoTracking`](architecture-review-prompts/09-efcore-projections-and-notracking.md) | Medium | Medium |
| 10 | [Add Blazor component tests (bUnit) and browser E2E (Playwright)](architecture-review-prompts/10-add-bunit-and-playwright-tests.md) | Medium | Medium |

### 1. Decompose `TablePlay.razor`
* **Why it matters:** `Components/Pages/TablePlay.razor` is **7,724 lines** with ~24 injected
  dependencies. It mixes rendering, SignalR event handling, and gameplay logic in one `@code`
  block. It is the single highest source of merge conflicts and review difficulty and is
  effectively impossible to unit-test.
* **Suggested approach:** Lift orchestration into a circuit-scoped `TablePlayViewModel`/state
  service (the codebase already has the seam via `Services/TableActions/*` and `TableRefreshPolicy`)
  and split the markup into child components (seats, board, action bar, showdown, side panels).

### 2. Extract showdown services
* **Why it matters:** `PerformShowdownCommandHandler.cs` (1,567 lines) and
  `TableStateBuilder.Showdown.cs` (1,576 lines) concentrate pot/side-pot math, hand evaluation, and
  persistence. Bugs here are high-severity (chip settlement) and the size blocks focused testing.
* **Suggested approach:** Extract `IPotAwardingService` and `IShowdownEvaluationService`; keep the
  handler as a thin orchestrator. Reuse for the `TableStateBuilder` projection path.

### 3. Remove the committed MediatR license key
* **Why it matters:** `Program.cs` line 160 hard-codes `cfg.LicenseKey = "eyJhbGci…"`. A licensing
  secret in version control is leaked permanently in git history and cannot be rotated cleanly.
* **Suggested approach:** Move to configuration/user-secrets/key vault; read via
  `builder.Configuration`. Treat the existing value as compromised and rotate.

### 4. Fix the FusionCache default duration
* **Why it matters:** `Program.cs` line 153 sets `Duration = TimeSpan.FromMilliseconds(2)` behind a
  `//TODO:ROB` that comments out the intended 5 minutes. The distributed cache stack
  (Redis + FusionCache) is wired up but effectively bypassed, so the caching investment yields
  almost nothing and adds serialization overhead per call.
* **Suggested approach:** Restore a sensible default (e.g. 5 min) and set explicit per-entry options
  for hot read paths; verify against the cache plan in `docs/InMemoryCacheImplementationPlan.md`.

### 5. Tighten the CORS policy
* **Why it matters:** `Program.cs` lines 92–98 use `SetIsOriginAllowed(_ => true)` **with**
  `AllowCredentials()`. That combination is unsafe for a credentialed real-money platform.
* **Suggested approach:** Bind an allow-list of origins from configuration per environment; keep the
  permissive policy for Development only.

### 6. Standardize `OneOf` → `IResult` mapping
* **Why it matters:** Endpoints repeat bespoke `result.Match(success => …, error => error.Code
  switch { … })` blocks (140 `MapGet`, 91 `MapPost`, etc.). Drift between slices produces
  inconsistent status codes and `ProblemDetails` shapes.
* **Suggested approach:** A shared `error → IResult` mapper (or endpoint filter) keyed on a common
  error-code contract, so every slice maps errors identically.

### 7. Consolidate the validation strategy
* **Why it matters:** FluentValidation is registered (`AddValidatorsFromAssemblyContaining`,
  `AddFluentValidationAutoValidation`) but there are **zero** `AbstractValidator` implementations in
  the solution; validation is actually performed by DataAnnotations on the `CardGames.Contracts`
  DTOs plus the new `AddValidation()`. Two half-wired strategies confuse contributors.
* **Suggested approach:** Pick DataAnnotations (current de-facto) or FluentValidation, and remove the
  unused wiring of the other.

### 8. Reduce per-variant slice duplication
* **Why it matters:** Each of ~15 game variants under `Features/Games/<Variant>/v1/Commands` carries
  its own near-identical `DealHands`/`ProcessBettingAction` handlers, while variant behaviour
  already lives behind `IGameFlowHandlerFactory`. This is thousands of duplicated lines and a
  maintenance multiplier for every cross-cutting change.
* **Suggested approach:** Route variants through the existing `Generic` slice + `GameFlow` handlers;
  keep per-variant slices only where behaviour genuinely diverges (already exempted in
  `GameVariantBoundaryTests`).

### 9. EF Core projections & tracking hygiene
* **Why it matters:** Handlers load full entity graphs (`Include`) and filter in memory; explicit
  `.Select()` projections are rare, and `AsNoTracking` is applied inconsistently (~15 sites). Read
  paths pay for change tracking and over-fetching.
* **Suggested approach:** Project read queries to DTOs, apply `AsNoTracking`/`AsSplitQuery`
  consistently on reads, and document the read-vs-write context convention.

### 10. Add component & browser tests
* **Why it matters:** Integration tests are strong (Testcontainers SQL Server), but there are no
  bUnit component tests for the large Blazor surface and no Playwright E2E for critical flows
  (join → deal → bet → showdown). The riskiest UI is the least covered.
* **Suggested approach:** Add a bUnit project for extracted components (synergizes with item 1) and a
  Playwright smoke suite for the core gameplay loop.

---

## Architectural Findings

### Solution structure (Positive baseline)
The solution cleanly separates concerns across projects: `CardGames.Core` / `CardGames.Core.French`
(card/deck primitives), `CardGames.Poker` (poker domain/evaluation), `CardGames.Poker.Api`
(application + persistence), `CardGames.Poker.Web` (Blazor Server), `CardGames.Contracts` +
`CardGames.Poker.Refitter` (shared DTOs / generated Refit client), `CardGames.Poker.Events`,
`CardGames.ServiceDefaults` + `CardGames.AppHost` (Aspire), and `CardGames.MigrationService`.
Dependency direction is sound: Web → Contracts/Refit → API; API → domain libraries.

### Vertical Slice Architecture
Slices follow a consistent `Endpoint → Command/Query → Handler (→ Mapper)` shape per feature folder
with `vN` versioning (e.g. `Features/Leagues/v1/Commands/CreateLeague`,
`Features/Profile/v1/Commands/UpdateFavoriteVariants`). Endpoints are discovered by reflection over
`[EndpointMapGroup]` types in `Features/MapFeatureEndpoints.cs` and registered via
`AddFeatureEndpoints`. This is a clean, low-ceremony slice implementation.

### Cross-slice coupling — the `Services/` "shared kernel"
The main deviation from slice ideals is a large shared services layer
(`Services/TableStateBuilder.*`, `GameStateBroadcaster`, `ContinuousPlayBackgroundService.*`,
`ActionTimerService`, `AutoActionService`) that many slices depend on. `TableStateBuilder` alone is
~3,100 lines across partials and is referenced by multiple game handlers and by the broadcasting
pipeline behavior. This is a reasonable trade-off for a real-time game (shared projection logic), but
it is effectively a god-service: changes ripple widely and unit isolation is hard. Recommend
continuing the existing partial-class decomposition into smaller, injectable collaborators (see
items 1, 2).

### MediatR pipeline
`Program.cs` registers five behaviors in a deliberate order: `Tracing → Logging →
GameStateBroadcasting → LobbyStateBroadcasting → LeagueGameCompletionSync`. Marker interfaces
(`IGameStateChangingCommand`, `ILobbyStateChangingCommand`, `IGameStateBroadcastResult`) drive the
cross-cutting behaviour cleanly. One smell: `GameStateBroadcastingBehavior` uses **reflection** to
detect the `OneOf` success branch (`responseType.GetGenericTypeDefinition().FullName.StartsWith
("OneOf.OneOf")` + reading `Index`). A small shared `IResultEnvelope`/`bool IsSuccess` contract would
remove the reflection and make success detection explicit (relates to item 6).

### Configuration management
Composition is entirely in a single ~380-line `Program.cs`. It works but mixes auth, CORS, SignalR,
caching, MediatR, OpenTelemetry, rate-limiting, API versioning, and storage. Extracting grouped
`IServiceCollection`/`WebApplication` extension methods (e.g. `AddPokerAuth`, `AddPokerCaching`,
`AddPokerObservability`) would improve readability and testability of the composition root. Note the
file also contains minor formatting inconsistency (tabs vs spaces around lines 124–125).

### Blazor architecture
Blazor Server with `@rendermode InteractiveServer` across pages, Refit clients (17 `AddRefitClient`
registrations) behind the documented `IGameApiRouter`, and SignalR hub clients
(`GameHubClient`, `LobbyHubClient`, `LeagueHubClient`, `NotificationHubClient`) as scoped services.
The router/refresh-policy abstractions are good. The weakness is component size and the absence of
code-behind (`.razor.cs`) files — all logic lives in `@code` blocks, so the largest pages
(`TablePlay` 7,724; `LeagueDetail` 2,854; `CreateTable` 1,905) are very hard to navigate and test.

### Scalability & deployment
Aspire AppHost orchestrates SQL Server, Redis, blob, and Service Bus; FusionCache is backplane-aware
and rate limiting is configured. The two structural scale limits are (a) **Blazor Server** circuits
(server-affinity, memory per connected user) and (b) the **in-process** `ContinuousPlayBackground
Service` hosted service, which advances games on the API host — under multi-instance deployment this
needs a single-owner/leasing strategy to avoid double-processing. Worth an explicit design note.

---

## Code Quality Findings

* **Large units (hotspots).** See the executive table. In addition to the showdown files,
  `Features/Games/Generic/v1/Commands/PerformShowdown/` contains the orchestration, and the
  per-variant `PerformShowdown`/`ProcessBettingAction` handlers are individually large (500–985
  lines each). These are the prime extraction candidates.
* **Duplication.** ~15 variant slices replicate `DealHands`/`ProcessBettingAction` structure even
  though variant logic is already centralized in `GameFlow` handlers (item 8).
* **Reflection-based control flow.** `GameStateBroadcastingBehavior` inspects `OneOf` internals by
  type-name string match — brittle to library upgrades (item 6).
* **Dead/unused infrastructure.** FluentValidation is registered with no validators present
  (item 7). Confirm whether the `Features/Testing` slice and `CardGames.Playground*` projects should
  ship in production builds.
* **Composition root size.** `Program.cs` is a single large method (see Configuration management).
* **Formatting drift.** Mixed tabs/spaces in `Program.cs` (lines ~124–125) and inconsistent
  indentation around the hosted-service registrations (lines ~128–132).

## Performance Findings

* **Caching effectively disabled.** FusionCache default duration is `2ms` (`Program.cs:153`,
  `//TODO:ROB`). The Redis + FusionCache stack is paid for but not used; this is the single biggest
  quick performance win (item 4).
* **Over-fetching / in-memory filtering.** GameFlow handlers commonly `Include` full graphs and then
  `Where`/`FirstOrDefault` in memory rather than projecting; `AsNoTracking` is applied inconsistently
  (~15 sites) (item 9).
* **Blazor Server payloads.** Very large components re-render large fragments over the circuit;
  decomposition (item 1) plus targeted `ShouldRender`/`@key` usage reduces diff size and CPU.
* **Showdown allocation.** The showdown path builds large intermediate collections per hand; once
  extracted (item 2), it becomes measurable/benchmarkable (the repo already has
  `CardGames.Core.Benchmarks` for BenchmarkDotNet).

## Security Findings

> Full treatment is in [`docs/SecurityReview.md`](SecurityReview.md). Highlights surfaced by this
> architecture pass:

* **Committed secret.** MediatR `LicenseKey` is hard-coded in `Program.cs:160` (item 3).
* **Permissive CORS.** `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` (`Program.cs:92–98`)
  (item 5).
* **Validation gap.** No active validator layer beyond DataAnnotations; consolidating (item 7)
  ensures every write endpoint has explicit, tested input validation.
* **Logging hygiene** was already addressed in `LoggingPipelineBehavior` per the observability audit
  (do not regress request-payload logging).

---

## Quick Wins (< 1 day each)

* Remove the committed MediatR license key → configuration (item 3).
* Restore a real FusionCache default duration (item 4).
* Replace the wildcard CORS origin check with a configured allow-list (item 5).
* Delete the unused FluentValidation registration (or add the missing validators) (item 7).
* Fix tab/space formatting in `Program.cs`; run `dotnet format`.
* Resolve or ticket the two remaining `TODO`s in `CardGames.Poker.Api`.

## Long-Term Refactoring Opportunities

* Decompose `TablePlay.razor` and the other 1,000+-line pages into components + view-models (item 1).
* Extract showdown/pot/evaluation services from the showdown hotspots (item 2).
* Collapse per-variant Deal/Betting slices behind the `GameFlow` handlers (item 8).
* Introduce a result-mapping convention and retire reflection-based success detection (item 6).
* Modularize the `Program.cs` composition root into grouped extension methods.
* Define a single-owner/leasing strategy for `ContinuousPlayBackgroundService` to enable
  multi-instance API deployment.
* Build out bUnit + Playwright test layers (item 10).

## Positive Findings

* **Disciplined vertical slices** with attribute-based endpoint discovery and `vN` versioning.
* **Consistent `OneOf` result modelling** (240 files) — explicit success/error contracts.
* **Well-ordered MediatR pipeline** for tracing, logging, and broadcasting cross-cutting concerns.
* **Documented, test-enforced boundaries**: `GameVariantBoundaryTests`, `TableStateAssemblyTests`,
  and design docs (`GameVariantBoundary.md`, `WebRouterDesign.md`, `TableRefreshPolicy.md`).
* **Real-infrastructure integration tests** via Testcontainers SQL Server and
  `WebApplicationFactory`.
* **Modern platform**: .NET 10, Aspire orchestration, OpenTelemetry, FusionCache, rate limiting,
  API versioning, Scalar/OpenAPI — the right building blocks are already in place.
* **Thoughtful real-time design**: centralized `TableRefreshPolicy`, `IGameApiRouter`, and partial
  `TableStateBuilder` show deliberate structuring of genuinely hard real-time problems.

---

*Deliverables for this review:* this report, the task list at
[`docs/architecture-review-prompts/README.md`](architecture-review-prompts/README.md), and one
implementation prompt per Top-10 item in the same folder.
