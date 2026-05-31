# Prompt 10 — Add Blazor component tests (bUnit) and browser E2E (Playwright)

> Addresses **Top-10 item 10** / Testing Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). Integration coverage is strong, but there
> are no Blazor component unit tests and no browser-level end-to-end tests for the core gameplay loop.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Add two missing test layers — **bUnit** for
Blazor components and **Playwright** for end-to-end smoke coverage — following the existing xUnit
conventions. Start small and high-value.

### Context (verify before editing)

- Front end: `src/CardGames.Poker.Web` (Blazor Server, `@rendermode InteractiveServer`). Largest
  components: `Components/Pages/TablePlay.razor` (7,724 lines), `Components/Shared/*` (e.g.
  `TableSeat.razor` 624, `DrawPanel.razor` 638, `ShowdownOverlay.razor` 1,304).
- Existing tests live under `src/Tests` (xUnit). Integration tests use `WebApplicationFactory` +
  Testcontainers SQL Server (`CardGames.IntegrationTests`). Web-logic unit tests already exist
  (`CardGames.Poker.Tests/Web/*`, e.g. `TableRefreshPolicyTests`, `GameVariantBoundaryTests`).
- There is currently **no** bUnit project and **no** Playwright project.

### Goal

1. **bUnit project.** Add `src/Tests/CardGames.Poker.Web.Tests` (xUnit + `bunit`) and add it to
   `CardGames.slnx`. Write component tests for **small, already-isolated** shared components first
   (`TableSeat`, `DrawPanel`, `ShowdownOverlay`), mocking injected services. This pairs with prompt
   **01** (TablePlay decomposition): test the extracted child components as they are created.
2. **Playwright E2E.** Add `src/Tests/CardGames.Poker.E2E` (`Microsoft.Playwright` + xUnit) covering
   the critical loop: sign in (dev seed user) → create/join table → deal → place a bet → reach
   showdown. Drive it against the Aspire-hosted app or a `WebApplicationFactory`-hosted instance.
   Keep it a **smoke** suite (a handful of high-signal flows), not exhaustive.
3. **CI wiring.** Ensure the new projects build in the normal solution build. Gate the Playwright
   suite behind a category/trait (e.g. `[Trait("Category","E2E")]`) so it can be run separately and
   cache the Playwright browser install in CI.

### Constraints

- Reuse existing fixtures/conventions (`ApiWebApplicationFactory`, `DatabaseSeeder`, dev seed users)
  rather than inventing new infrastructure.
- Keep E2E deterministic: use the dev user seed and avoid timing flakiness (await SignalR/UI state via
  Playwright `expect`/auto-waiting).

### Acceptance criteria

- A bUnit project exists with passing tests for at least 2–3 shared components.
- A Playwright project exists with at least one passing critical-path smoke test, runnable via a
  dedicated category filter.
- Both projects are in `CardGames.slnx` and build from `src` (`dotnet build CardGames.slnx
  --no-restore`).
