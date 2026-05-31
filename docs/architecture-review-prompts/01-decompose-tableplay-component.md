# Prompt 01 — Decompose `TablePlay.razor` into components + a table view-model

> Addresses **Top-10 item 1** / Blazor & Maintainability Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). `TablePlay.razor` is a single
> **7,724-line** component with ~24 injected dependencies that mixes rendering, SignalR wiring, and
> gameplay logic.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 Blazor Server app (`src/CardGames.Poker.Web`). Break the
monolithic `TablePlay.razor` into focused child components plus a circuit-scoped view-model/state
service, **without changing observable UI behaviour**. This is large — work in small, shippable
increments and keep the app runnable at every step.

### Context (verify before editing)

- `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` — **7,724 lines**, `@rendermode
  InteractiveServer`, ~24 `@inject` dependencies (Refit API clients, hub clients, state services,
  loggers). All logic is in one `@code` block; there is **no** `.razor.cs` code-behind.
- Existing seams to build on:
  - `Services/TableActions/*` — `TableActionExecutor`, `TableActionResult`, `TableActionError`
    (guard/loading/refresh/log/notify around actions).
  - `Services/TableRefreshPolicy.cs` + `TablePlay.ApplyRefreshPolicyAsync` (documented in
    `docs/TableRefreshPolicy.md`) — centralized refresh decisions.
  - `Services/IGameApiRouter.cs` — per-action dispatch to the right API (documented in
    `docs/WebRouterDesign.md`).
  - `GameHubClient` / `LobbyHubClient` / `NotificationHubClient` — SignalR as scoped services.
- Existing shared components to model after: `Components/Shared/TableSeat.razor`,
  `TableCanvas.razor`, `DrawPanel.razor`, `ShowdownOverlay.razor`.

### Goal

1. **Introduce a code-behind + view-model.** Move the `@code` logic into a `TablePlay.razor.cs`
   partial, then lift orchestration/state into a circuit-scoped `TablePlayViewModel` (or
   `TablePlayState`) service that exposes state + commands and raises change notifications. The
   component subscribes and renders; the view-model owns API-router calls, hub-event handling, and
   refresh-policy application.
2. **Split the markup into child components**, each with a tight `[Parameter]` surface and `EventCallback`s,
   e.g.:
   - `TableBoard` / community-card area,
   - seat ring (reuse/extend `TableSeat`),
   - `ActionBar` (bet/call/fold/raise controls),
   - showdown area (reuse `ShowdownOverlay`),
   - side panels (chat/history/info).
   Pass state down; raise actions up via `EventCallback` to the view-model.
3. **Reduce re-render cost.** Use `@key` on seat/card lists and override `ShouldRender` (or use
   immutable parameter records) so a single seat update does not re-diff the whole 7k-line tree.
4. Keep the page route and external behaviour identical.

### Constraints

- **Behaviour-preserving.** No change to gameplay flow, refresh behaviour, or hub handling — only
  structure. Reuse `TableActionExecutor` and `TableRefreshPolicy`; do not reinvent them.
- Migrate **incrementally**: extract one region/child component at a time, build and smoke-test, then
  continue. Do not attempt a single big rewrite.
- Keep DI registrations consistent with the existing `Program.cs` (scoped per circuit).
- Honour the documented designs in `docs/TableRefreshPolicy.md` and `docs/WebRouterDesign.md`.

### Tests / verification

- This pairs with prompt **10**: as each child component is extracted, add a bUnit test for it
  (mock injected services). At minimum, smoke-test the table flow manually after each increment.
- Keep `TableRefreshPolicyTests` green; if refresh logic moves into the view-model, keep it covered.

### Acceptance criteria

- `TablePlay.razor` is dramatically smaller (markup + a thin code-behind), with gameplay orchestration
  in a `TablePlayViewModel` and rendering split across child components.
- No behavioural/route changes; the table plays identically.
- Build green from `src` (`dotnet build CardGames.slnx --no-restore`) and any added bUnit tests pass.
