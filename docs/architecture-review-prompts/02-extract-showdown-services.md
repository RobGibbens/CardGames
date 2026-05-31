# Prompt 02 — Extract showdown pot-award & evaluation services

> Addresses **Top-10 item 2** / Code Quality & Maintainability Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). The showdown logic is concentrated in two
> ~1,570-line files that mix evaluation, pot/side-pot math, persistence, and UI projection.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution. Reduce the size and responsibility of the
showdown hotspots by extracting focused, injectable services — **without changing chip-settlement
behaviour**. This is a high-risk area (money/chips); proceed in small, test-guarded steps.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/
  PerformShowdownCommandHandler.cs` — **1,567 lines**. Orchestrates: load game/players/cards/pots,
  validate phase, resolve variant via `IGameFlowHandlerFactory` + `IHandEvaluatorFactory`, group
  cards, evaluate hands, award main + side pots, update chip stacks, record hand history, handle
  win-by-fold / dead-hand / inline-showdown paths, and persist.
- `src/CardGames.Poker.Api/Services/TableStateBuilder.Showdown.cs` — **1,576 lines**. Builds the
  showdown **projection** (what the UI sees) for every variant and overlaps conceptually with the
  handler's evaluation logic.
- Settlement helpers already exist: `Services/HandSettlementService`, `Services/HandHistoryRecorder`,
  `Services/PlayerChipWalletService`, and the static `TableHandEvaluators`.
- Behaviour is locked by integration tests (e.g. `Tests/CardGames.IntegrationTests/**`, showdown and
  side-pot tests) and `TableStateAssemblyTests`.

### Goal

Extract two cohesive services and make the handler a thin orchestrator:

1. **`IShowdownEvaluationService`** — given the game, player hole cards, community/special cards, and
   the variant evaluator, returns ranked results per player (who wins which pot eligibility). Pure-ish
   and unit-testable.
2. **`IPotAwardingService`** — given evaluation results and the pot/side-pot structure, computes
   chip awards (including all-in side pots and split pots) and the per-player deltas. No DB access;
   takes inputs and returns a result object the handler persists.
3. Keep `PerformShowdownCommandHandler` responsible only for: load → validate → call the two services
   → persist via existing `HandSettlementService`/`HandHistoryRecorder` → build the success result.
4. Reuse the **same** evaluation/award services from `TableStateBuilder.Showdown.cs` so the projection
   and the settlement agree by construction (eliminates the duplicated showdown reasoning).

### Constraints

- **No behaviour change** to chip math, side-pot splitting, or hand history. Capture current outputs
  first (characterization tests) and keep them green.
- Register new services in `Program.cs` following the existing `AddScoped` conventions.
- Keep the special-case variants (e.g. Screw Your Neighbor inline showdown, Kings-and-Lows) working —
  route them through the same services or keep their existing `IGameFlowHandler` hook, but document
  which path each variant uses.
- Do this **before** prompt 08 (variant de-duplication), which depends on a clean generic showdown.

### Tests / verification

- Add focused unit tests for `IPotAwardingService` covering: single winner, split pot, multiple
  all-in side pots, and dead hand. Add unit tests for `IShowdownEvaluationService` per board type
  (community / draw / stud).
- The existing integration showdown/side-pot tests must remain green unchanged.

### Acceptance criteria

- `PerformShowdownCommandHandler` is materially smaller (orchestration only) and delegates to the two
  new services.
- The two services have direct unit-test coverage.
- `TableStateBuilder.Showdown.cs` reuses the extracted evaluation/award logic (no duplicated showdown
  reasoning).
- Build + integration tests green from `src`.
