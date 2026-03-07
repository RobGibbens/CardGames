# Rusty History

## Project Learnings (from import)
- Project: CardGames (.NET 10 card games platform with API, Blazor Web, and tests).
- Requested by: Rob Gibbens.
- Primary architecture references: docs/ARCHITECTURE.md and README.md.

## Learnings

- Recorded team-level decision in `.ai-team/decisions.md` to prioritize OAuth providers first on auth pages, with local auth preserved as secondary fallback and no behavior changes.
- Designed `docs/CashierFeatureDesign.md` for an account-level Cashier feature: add `Cashier` nav item, new `/cashier` page (balance hero + add chips + ledger), and Profile-scoped MediatR APIs/contracts for summary, add-chips, and paged ledger while keeping existing table/game chip flows unchanged.
- Session outcome captured by Scribe: cashier design decisions were merged from inbox into `.ai-team/decisions.md` and orchestration/session logs were recorded under `.ai-team/orchestration-log` and `.ai-team/log`.
- Consolidated Leagues design direction into `docs/LeaguesFeatureDesign.md` as approval baseline: account-scoped league boundary, event-based mutable membership with projections, invite-link security controls (hash/expiry/revoke/rate-limit), contract regeneration-first API direction, explicit MVP/non-goals, and implementation phases mapped to `CardGames.Poker`, `CardGames.Poker.Api`, `CardGames.Contracts`, `CardGames.Poker.Web`, and `src/Tests`.
- Design-review decision set for Leagues (boundary, temporal membership, RBAC bootstrap/promotion, invite-link lifecycle, season + one-off event support) was merged into canonical `.ai-team/decisions.md` for implementation reference.
- Implemented issue #216 governance P0 flows in `Leagues/v1` with minimal MediatR endpoints for ownership transfer, admin demotion, and member removal; enforced manager/governance-capable invariants in each operation and extended membership history auditing with demotion/ownership-transfer event types plus integration coverage for success + safety-conflict paths.
- Session record updated for #224 quality-gate direction: Leagues release progression now depends on API integration P0 journey coverage and CI enforcement in `squad-ci`, so implementation changes should preserve deterministic moderation and conflict semantics.
- Completed #216 end-to-end by widening league governance member-administration authority so active admins (in addition to managers) can promote/demote admins while invariants still block orphaned governance states; added/updated integration coverage in API and command handlers to validate audit trail + conflict semantics.
- Ran focused pre-implementation design review for `My Clubs` in `src/CardGames.Poker.Web/Components/Pages/Leagues.razor`; approved a strict look-and-feel-only refresh that preserves all existing UX behavior, role/action semantics, and loading/empty state flows.
- Captured implementation guardrails and participant action split (Arwen/Galadriel) in `.ai-team/decisions/inbox/rusty-my-clubs-redesign.md` to prevent scope drift (no new features/components/tokens, maintain existing classes and functionality).
- 2026-02-19 (team update): design-review scope lock for `My Clubs` is now canonical in `.ai-team/decisions.md`; any follow-on changes must remain style-only within existing classes/components and preserve all behavior and permission semantics.
- 2026-02-19 (lead update): produced a new `My Clubs` rethink baseline in `.ai-team/decisions/inbox/rusty-my-clubs-rethink-baseline.md` that supersedes prior polish-only constraints for this section; defined task-first IA, attention-first interaction flow, strict no-backend-change guardrails, and explicit implementation split (Arwen = structure/IA, Galadriel = visual hierarchy/styling).
- 2026-02-19 (team update): My Clubs full-rethink directive and baseline are now merged from inbox into canonical `.ai-team/decisions.md`; follow-on direction should reference canonical baseline rather than inbox artifacts.
- 2026-02-19 (lead update): captured new inbox baseline at `.ai-team/decisions/inbox/rusty-my-clubs-command-center-baseline.md` for a full `My Clubs` Command Center card-grid redesign: consolidated title+icon refresh rail, stats pills (Total/Admin/Pending), removal of Quick Open and per-card Leave actions, Open-first full-width card CTA, pending-pill state emphasis, and strict behavior/data parity with no backend/API changes.
- 2026-02-19 (team update): Command Center baseline decision has been merged from inbox into canonical `.ai-team/decisions.md`; use canonical entry for subsequent implementation/review references.
- 2026-03-02 (lead analysis): Produced comprehensive Dealer's Choice architecture analysis. Key findings:
  - `Game.GameTypeId` (non-nullable FK) is the central constraint — DC tables need per-hand game type resolution, not per-table.
  - `ContinuousPlayBackgroundService.StartNextHandAsync` resolves `flowHandler` from `game.GameType?.Code` once per hand — DC needs to swap this between hands.
  - `MoveDealer(game)` is called in every showdown handler (generic + game-specific) — DC needs a separate `DealerChoiceDealerPosition` to avoid corruption from Kings and Lows internal rotation.
  - Kings and Lows has its own `PerformShowdownCommandHandler` with internal `MoveDealer` at `Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` — this call rotates `game.DealerPosition` multiple times during a single DC "turn".
  - `Phases` enum needs `WaitingForDealerChoice` to pause continuous play and prompt the DC dealer via UI (similar to how `WaitingForPlayers` pauses).
  - `CreateGameCommand` currently requires `GameCode` string — DC tables skip this, deferring game type selection to first dealer.
  - `CreateTable.razor` Step 1 (variant selection) must support a "Dealer's Choice" card alongside real variants.
  - Decisions recorded in `.squad/decisions/inbox/rusty-dealers-choice-architecture.md`.
- 2026-03-07 (Irish Hold 'Em Phase 3 release): Aragorn audited all 44 PRD acceptance criteria. Found critical blocker — `UsesSharedCommunityCards()` in `PerformShowdownCommandHandler.cs` did not include `IrishHoldEmCode`, causing showdown to silently skip Irish players. Fix applied, 81 Irish tests verified (60 unit + 21 integration). Release approved after fix. PRD status: Released.
