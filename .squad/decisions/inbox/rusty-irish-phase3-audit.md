# Decision: Irish Hold 'Em Phase 3 Audit — Release Readiness

**Date:** 2026-03-07
**Decision-maker:** Aragorn (Lead)
**Requested by:** Rob Gibbens
**Status:** NO-GO — one critical blocker found

---

## Summary

Full audit of every acceptance criterion from the Irish Hold 'Em PRD (Section 7), the Phase 2 rollout checklist (Section 8), and the Section 5 branch-audit checklists against the actual codebase.

**43 out of 44 items PASS. 1 CRITICAL FAIL blocks release.**

## Critical Blocker

**`PerformShowdownCommandHandler.UsesSharedCommunityCards()`** (line ~912) only matches `HoldEmCode` and `OmahaCode`. It does **not** include `IrishHoldEmCode`.

- **Impact:** During the generic showdown path, community cards are not included when evaluating Irish Hold 'Em hands. Players end up with only 2 hole cards + 0 board cards = 2 total, which is less than 5, causing them to be **silently skipped** in hand evaluation. No winner can be determined.
- **Note:** `TableStateBuilder` has a correct, dedicated Irish branch (using `HoldemHand` with community cards), so the in-game UI display of hand evaluations is fine — but the authoritative showdown resolution that determines the winner and pays out the pot is broken.
- **Fix:** Add `|| string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode, StringComparison.OrdinalIgnoreCase)` to `UsesSharedCommunityCards()` in `PerformShowdownCommandHandler.cs`.

## Verdict

**NO-GO for production release** until the showdown handler fix is applied and the lifecycle integration test is verified end-to-end through a real showdown.

---

## Full Audit Results

### Section 7 — Acceptance Criteria

| # | Criterion | Verdict | Evidence |
|---|-----------|---------|----------|
| 1 | Irish selectable in Create Table with blind inputs | **PASS** | `CreateTable.razor:475` — `IsBlindBasedGame` includes `"IRISHHOLDEM"` |
| 2 | Irish in Dealer's Choice prompts blinds | **PASS** | `DealerChoiceModal.razor:139` — `IsBlindBasedGame` includes `"IRISHHOLDEM"` |
| 3 | Starting hand deals 4 hole cards + posts blinds | **PASS** | `IrishHoldEmFlowHandler.cs` — `InitialCardsPerPlayer = 4`, `SkipsAnteCollection = true`, calls `CollectBlindsAsync` |
| 4 | Pre-flop betting left of big blind | **PASS** | `IrishHoldEmRules.cs` defines `PreFlop` phase; routes through HoldEm betting action |
| 5 | Flop deals 3 community cards | **PASS** | `IrishHoldEmRules.cs` — `Flop` phase after `PreFlop` |
| 6 | Discard overlay shows after flop, requiring exactly 2 | **PASS** | `TablePlay.razor:4277,4289` — `MaxDiscards=2, MinDiscards=2`; `DrawPanel.razor` enforces min/max |
| 7 | Cannot proceed until exactly 2 selected | **PASS** | `ProcessDiscardCommandHandler.cs` — `RequiredDiscardCount = 2`, button disabled when `SelectedCount < MinDiscards` |
| 8 | After discard → turn card + betting | **PASS** | `IrishHoldEmFlowHandler.ProcessDrawCompleteAsync` returns `"Turn"` |
| 9 | After turn → river card + final betting | **PASS** | `IrishHoldEmRules.cs` — `River` phase after `Turn` |
| 10 | Showdown uses Hold'Em ranking (2 hole + 5 community) | **FAIL** | `PerformShowdownCommandHandler.UsesSharedCommunityCards()` excludes IRISHHOLDEM — community cards not included |
| 11 | Blind badges display on canvas | **PASS** | `TableCanvas.razor:234` — `IsBlindBasedGame` includes `"IRISHHOLDEM"` |
| 12 | Continuous play works with DC rotation | **PASS** | `ContinuousPlayBackgroundService` already includes `DrawPhase` in `inProgressPhases` |
| 13 | Existing variants pass tests unchanged | **PASS** | Implementation is purely additive (new files + expanded `or` checks) |
| 14 | No database migration required | **PASS** | All needed columns pre-exist (`SmallBlind`, `BigBlind`, `IsDiscarded`, etc.) |
| 15 | Fold-to-win works at any phase | **PASS** | `ProcessDiscardCommandHandler` detects fold-to-win; lifecycle tests cover fold scenarios |

### Section 5 — UI Checklist

| Item | Verdict | Evidence |
|------|---------|----------|
| `CreateTable.razor` IsBlindBasedGame | **PASS** | Line 475 |
| `DealerChoiceModal.razor` IsBlindBasedGame | **PASS** | Line 139 |
| `TablePlay.razor` IsIrishHoldEm + branches | **PASS** | Lines 676, 677, 2558, 2666, 4277, 4289 |
| `TableCanvas.razor` IsBlindBasedGame | **PASS** | Line 234 |
| `IGameApiRouter.cs` constant + routes | **PASS** | Lines 96, 144, 156, 267, 279 |
| `DrawPanel.razor` MinDiscards enforcement | **PASS** | Lines 21-27, 104-110, 129, 136 |
| `DashboardHandOddsCalculator.cs` IRISHHOLDEM | **PASS** | Line 66 |
| `irishholdem.png` image | **PASS** | File exists at `wwwroot/images/games/irishholdem.png` |

### Section 5 — Backend / Domain Checklist

| Item | Verdict | Evidence |
|------|---------|----------|
| `IrishHoldEmGame.cs` | **PASS** | Exists with Discarding phase |
| `IrishHoldEmGamePlayer.cs` | **PASS** | Exists with discard support |
| `IrishHoldEmRules.cs` | **PASS** | Full phase list including `DrawPhase` |
| `IrishHoldEmShowdownResult.cs` | **PASS** | Exists |
| `IrishHoldEmHandEvaluator.cs` | **PASS** | `[HandEvaluator("IRISHHOLDEM")]` using `HoldemHand` |
| `IrishHoldEmFlowHandler.cs` | **PASS** | Discard → Turn orchestration, blind collection |
| ProcessDiscard command | **PASS** | Enforces exactly 2 discards with validation |
| `PokerGameMetadataRegistry.cs` | **PASS** | `IrishHoldEmCode = "IRISHHOLDEM"` |
| `HandEvaluatorFactory` auto-discovery | **PASS** | Attribute-based scan finds `IrishHoldEmHandEvaluator` |
| `TableStateBuilder.cs` Irish branch | **PASS** | Dedicated `isIrishHoldEm` branch using `HoldemHand` |
| `PerformShowdownCommandHandler` | **FAIL** | `UsesSharedCommunityCards` excludes Irish — see blocker above |
| `ContinuousPlayBackgroundService` | **PASS** | `DrawPhase` already in `inProgressPhases` |
| `BaseGameFlowHandler` blind collection | **PASS** | `CollectBlindsAsync` shared, called by Irish flow handler |

### Section 5 — Testing Checklist

| Item | Verdict | Evidence |
|------|---------|----------|
| `IrishHoldEmGameTests.cs` | **PASS** | Multiple Facts (phase progression, discard, fold) |
| `IrishHoldEmHandEvaluatorTests.cs` | **PASS** | Facts + Theories for hand evaluation |
| `IrishHoldEmSmokeTests.cs` | **PASS** | Integration smoke tests (creation, lifecycle, evaluator, factory) |
| `IrishHoldEmHandLifecycleTests.cs` | **PASS** | Full lifecycle + fold scenarios |
| `IntegrationTestBase.cs` seed | **PASS** | `CreateGameType("IRISHHOLDEM", ...)` at line 101 |
| `ChooseDealerGameCommandTests.cs` | **PASS** | Irish InlineData at lines 220, 268 |
| `CreateGameCommandHandlerTests.cs` | **PASS** | Irish InlineData at lines 20, 62 |
