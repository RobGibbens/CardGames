# Architecture Review — Implementation Prompts & Task List

These are ready-to-use **GitHub Copilot** prompts for implementing the recommendations in
[`docs/ArchitectureReview.md`](../ArchitectureReview.md). Each file is a self-contained prompt: paste
it into Copilot Chat (agent mode) in this repository, or open it as context, and Copilot will have
enough detail to implement the change against the real project structure.

Each prompt includes: goal, why it matters, exact files to touch (with line references), the
concrete approach, tests to add/run, and acceptance criteria. The parent report is analysis only —
these prompts are where the work is actually specified.

## Task list

Track progress by checking items off as they land. Ordered to do the low-risk quick wins first, then
the larger structural refactors.

- [ ] **03** — [Remove the committed MediatR license key](03-remove-committed-mediatr-license-key.md) · Security · Impact High · Effort Low
- [ ] **04** — [Fix the FusionCache default duration (`2ms` `TODO:ROB`)](04-fix-fusioncache-default-duration.md) · Performance/Config · Impact High · Effort Low
- [ ] **05** — [Tighten the wide-open SignalR CORS policy](05-tighten-cors-policy.md) · Security · Impact High · Effort Low
- [ ] **07** — [Consolidate the validation strategy (remove unused FluentValidation)](07-consolidate-validation-strategy.md) · Code Quality · Impact Medium · Effort Low
- [ ] **06** — [Standardize `OneOf` → `IResult` mapping across endpoints](06-standardize-result-mapping.md) · API Consistency · Impact Medium · Effort Medium
- [ ] **09** — [Add EF Core projections, split queries, and consistent `AsNoTracking`](09-efcore-projections-and-notracking.md) · Performance · Impact Medium · Effort Medium
- [ ] **10** — [Add Blazor component tests (bUnit) and browser E2E (Playwright)](10-add-bunit-and-playwright-tests.md) · Testing · Impact Medium · Effort Medium
- [ ] **02** — [Extract showdown pot-award & evaluation services](02-extract-showdown-services.md) · Maintainability · Impact High · Effort High
- [ ] **08** — [Collapse duplicated per-variant Deal/Betting slices](08-reduce-per-variant-slice-duplication.md) · Maintainability · Impact High · Effort High
- [ ] **01** — [Decompose `TablePlay.razor` into components + view-model](01-decompose-tableplay-component.md) · Maintainability · Impact High · Effort High

## Dependency notes

- **01** (TablePlay decomposition) and **10** (bUnit tests) are synergistic — extract components
  first, then test them.
- **02** (showdown services) and **08** (variant de-duplication) both touch the
  `Features/Games/**` slices and `GameFlow` handlers; do **02** first so **08** can route variants
  through the cleaned-up generic showdown path.
- **06** (result mapping) removes the reflection in `GameStateBroadcastingBehavior`; coordinate with
  any change to the `OneOf` success contract.

## Shared conventions all prompts must follow

- **Smallest change that achieves the goal.** Do not rewrite working code wholesale; refactor behind
  existing seams (`Services/TableActions/*`, `IGameApiRouter`, `IGameFlowHandlerFactory`,
  `TableRefreshPolicy`).
- **Preserve behaviour.** These are structural/quality changes; keep observable API/UI behaviour
  identical unless the item explicitly changes a value (e.g. cache duration, CORS origins).
- **Build** from `src`: `dotnet restore CardGames.slnx` then `dotnet build CardGames.slnx
  --no-restore`.
- **Test** the affected area before running everything, e.g.
  `dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --no-build` from `src`;
  integration tests live in `Tests/CardGames.IntegrationTests` (Testcontainers SQL Server).
- **Respect the guard tests**: `GameVariantBoundaryTests`, `TableStateAssemblyTests`,
  `TableRefreshPolicyTests` encode boundaries — keep them green.
- **No secrets in source.** Read configuration via `builder.Configuration` / user-secrets / key
  vault.
- **Match existing test conventions** (xUnit + the patterns already in
  `src/Tests/CardGames.Poker.Tests` and `src/Tests/CardGames.IntegrationTests`).
