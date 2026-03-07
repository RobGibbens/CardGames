# PRD: Omaha (High) Enablement  
**Repo:** CardGames  
**Date:** 2026-03-06  
**Status:** Final

## 1) Goals / Non-Goals

**Goals**
- Ship Omaha (high-only) using existing Texas Hold’Em architecture patterns, with explicit Omaha gameplay differences.
- Enforce Omaha hand rules: 4 hole cards dealt; final hand must use exactly 2 hole + 3 board cards.
- Make Omaha selectable in Create Table and Dealer’s Choice flows.
- Preserve existing game behavior and contracts for all currently shipped variants.

**Non-Goals**
- No Omaha Hi/Lo, split-pot Omaha8, or custom betting structures in this phase.
- No redesign of overall game flow architecture beyond Omaha enablement.
- No large UI redesign; only branch/generalization required for Omaha support.

---

## 2) Current-State Audit (Exists vs Missing)

### A. UI branch points (required concrete findings)

| File | Current state (concrete) | Gap for Omaha |
|---|---|---|
| [src/CardGames.Poker.Web/Components/Pages/CreateTable.razor](src/CardGames.Poker.Web/Components/Pages/CreateTable.razor) | `IsGameAvailable(...)` hardcoded whitelist excludes Omaha name; `_hasBlinds = variant.Code == "HOLDEM"`; blind fields only shown when `_hasBlinds`; create payload sets blind values only when `_hasBlinds`. | Omaha is effectively unavailable in Create Table and cannot configure blinds there. |
| [src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor](src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor) | `IsBlindBasedGame(...)` returns true only for `"HOLDEM"`; non-HOLDEM variants use ante/min-bet inputs. | Omaha in Dealer’s Choice receives ante/min-bet UI instead of blind UI. |
| [src/CardGames.Poker.Web/Components/Pages/TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor) | `IsHoldEm` drives start/showdown branch; start uses `HoldEmApi.HoldEmStartHandAsync(...)`; showdown uses `HoldEmApi.HoldEmPerformShowdownAsync(...)`; non-HoldEm falls back to Five Card Draw start/showdown path; overlay receives `IsHoldEm`. | Omaha currently routes to non-HoldEm fallback path, which is incorrect for community-card blind game. |
| [src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor) | Blind badges use `IsHoldEmGame` check only; SB/BB seat calculations gated by HoldEm-only predicate. | Omaha blind badges/seat indicators never show. |
| [src/CardGames.Poker.Web/Services/IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs) | Constants/routes include HOLDEM but not OMAHA; unknown game code falls back to Five Card Draw for betting/draw. | Omaha betting/draw routing is absent; fallback risks invalid command paths. |
| [src/CardGames.Poker.Web/Program.cs](src/CardGames.Poker.Web/Program.cs) | Registers `IHoldEmApi` and HoldEm wrapper; no Omaha Refit registration/wrapper. | No Omaha client wiring in web DI. |

### B. Backend/domain branch points (required concrete findings)

| File | Current state (concrete) | Omaha impact |
|---|---|---|
| [src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs) | Contains `OmahaCode = "OMAHA"` and reflection-based metadata discovery. | Omaha metadata registration already exists. |
| [src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs) | Reflection-based rules discovery from `IPokerGame`. | Omaha rules are discoverable if class/rules are valid. |
| [src/CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs](src/CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs) | Validates registered code then parses global `Phases` enum. | Works for shared phase names; no Omaha-specific branching. |
| [src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs](src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs) | Reflection auto-registers all `IGameFlowHandler`; default fallback is Five Card Draw. | Omaha handler is discoverable; unknown code still falls back to FCD. |
| [src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs) | Community card dealing, 2 hole cards, blinds, initial phase `CollectingBlinds`, custom auto-betting command override. | Serves as implementation baseline for Omaha parity. |
| [src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs) | Exists; 4 hole cards; blinds collection; initial phase currently `Dealing`. | Present but not fully integrated end-to-end with web/API routes and evaluator path. |
| [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs) | Uses flow factory from `game.GameType?.Code`; generic orchestration. | Omaha can start through generic flow if called via generic endpoint. |
| [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs) | Uses evaluator factory; positional branch currently passes `holeCards.Take(2)`; player-card query is player-linked cards only. | Current generic showdown path is not Omaha-safe for 4-hole-card exact-two logic. |
| [src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs) | Accepts any registered game code; persists `SmallBlind`/`BigBlind` if supplied. | Omaha creation possible at command level once UI/routes pass correct blind data. |
| [src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs) | Validates game code exists; validates blind values only if provided; applies blinds when present. | Supports Omaha mechanically, but UI currently does not provide blind inputs for Omaha. |
| [src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs) | Includes community-card phases and comments for Hold’Em/Omaha; uses flow handler per `CurrentHandGameTypeCode`/`GameType`. | Omaha lifecycle support is partially present in background service orchestration. |
| [src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs](src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs) | Reflection/attribute evaluator lookup; unknown game types fallback to draw evaluator. | No Omaha evaluator registration means fallback risk. |
| [src/CardGames.Poker/Evaluation/Evaluators/HoldemHandEvaluator.cs](src/CardGames.Poker/Evaluation/Evaluators/HoldemHandEvaluator.cs) | `[HandEvaluator("HOLDEM")]`; 2-hole-card semantics. | Hold’Em-only evaluator; Omaha-specific evaluator missing. |
| [src/CardGames.Poker/Games/Omaha/OmahaGame.cs](src/CardGames.Poker/Games/Omaha/OmahaGame.cs) | Omaha game class exists with metadata (4 hole cards, blinds). | Core domain gameplay exists. |
| [src/CardGames.Poker/Games/Omaha/OmahaRules.cs](src/CardGames.Poker/Games/Omaha/OmahaRules.cs) | Rules exist, description states exactly two hole cards + three board cards. | Domain rule statement exists; evaluator/runtime enforcement integration missing. |
| [src/CardGames.Poker/Games/HoldEm/HoldEmGame.cs](src/CardGames.Poker/Games/HoldEm/HoldEmGame.cs) | Mature Hold’Em implementation with blind rules and heads-up handling. | Reference pattern to mirror. |
| [src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs](src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs) | Hold’Em phases include `CollectingBlinds`/`Dealing` and betting progression. | Baseline for Omaha phase-model parity. |

### C. Testing/regression points (required concrete findings)

| File | Current state (concrete) | Gap |
|---|---|---|
| [src/Tests/CardGames.Poker.Tests/Hands/OmahaHandTests.cs](src/Tests/CardGames.Poker.Tests/Hands/OmahaHandTests.cs) | Strong unit coverage for exact-two-hole-card behavior. | Keep; add evaluator-level assertions once Omaha evaluator is introduced. |
| [src/Tests/CardGames.Poker.Tests/Games/OmahaGameTests.cs](src/Tests/CardGames.Poker.Tests/Games/OmahaGameTests.cs) | Game-level Omaha unit tests exist. | Keep; align with any flow/rules phase normalization changes. |
| [src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs](src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs) | Hold’Em lifecycle integration baseline. | Must remain green as regression guard. |
| [src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs) | Multi-game tests include Hold’Em but not Omaha in variant matrix. | Add Omaha Dealer’s Choice selection + blind assertion cases. |
| [src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs) | Variant matrix includes Hold’Em, not Omaha. | Add Omaha create-game integration cases with blind values. |
| [src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs](src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs) | Dealer’s Choice continuous-play coverage exists; no Omaha-specific blind scenario. | Add Omaha-in-DC continuation/start-path scenarios. |

---

## 3) Detailed Requirements by Layer

### Domain / Gameplay
- Reuse Hold’Em community-card flow pattern for Omaha: blind-based, 4 betting rounds (pre-flop/flop/turn/river), showdown.
- Enforce Omaha rule at evaluation time: exactly 2 of player’s 4 hole cards and exactly 3 board cards.
- Ensure phase progression for Omaha is internally consistent with flow handler + rules descriptors.
- Preserve blind posting behavior consistency (including heads-up seat logic parity with existing blind infrastructure).

### API / Contracts
- Add Omaha API surface parallel to Hold’Em pattern (start hand, process betting action, showdown) or equivalent generic route consumption that is Omaha-safe.
- Add Omaha client contract(s) in Contracts project and register in web DI.
- Ensure web routers do not fall back to Five Card Draw for Omaha actions.
- Keep existing Hold’Em and other variant endpoints intact.

### DB / Seeding
- No schema change required for core Omaha enablement (blind fields already exist on game entities and logs).
- Ensure `GameTypes` includes/creates OMAHA consistently in all environments (runtime auto-create path is present via create/choose handlers).
- Ensure Dealer’s Choice hand logs capture blind values for Omaha selections as for Hold’Em.

### Web
- Enable Omaha as selectable in Create Table.
- Treat Omaha as blind-based in Create Table and Dealer’s Choice modal.
- In Table Play, route Omaha start/showdown/action paths explicitly (or via Omaha-safe generic routing), not Five Card Draw fallback.
- Show blind badges for Omaha in table canvas.
- Keep existing game info overlay behavior while generalizing blind-based context where needed.

---

## 4) Game-Type Branch Audit Checklist

### UI checklist
- [ ] [src/CardGames.Poker.Web/Components/Pages/CreateTable.razor](src/CardGames.Poker.Web/Components/Pages/CreateTable.razor): remove Hold’Em-only availability/blind gating; include Omaha.
- [ ] [src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor](src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor): blind-based predicate must include Omaha.
- [ ] [src/CardGames.Poker.Web/Components/Pages/TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor): add Omaha branch points for start/showdown and any Hold’Em-only display toggles.
- [ ] [src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor): blind badges based on blind-based game, not Hold’Em-only.
- [ ] [src/CardGames.Poker.Web/Services/IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs): add Omaha constants/routes; remove unsafe fallback for Omaha.
- [ ] [src/CardGames.Poker.Web/Program.cs](src/CardGames.Poker.Web/Program.cs): register Omaha Refit client/wrapper/router wiring.

### Backend/domain checklist
- [ ] [src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs): verify Omaha metadata remains discoverable.
- [ ] [src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs): verify Omaha rules discovery.
- [ ] [src/CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs](src/CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs): confirm phase resolution compatibility for Omaha phases.
- [ ] [src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs](src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs): verify Omaha handler registration and no unintended fallback.
- [ ] [src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs): baseline parity reference.
- [ ] [src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs): finalize Omaha phase/dealing behavior consistency.
- [ ] [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs): ensure Omaha start works under generic orchestration.
- [ ] [src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs): make showdown evaluator path Omaha-safe.
- [ ] [src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs): enforce/create Omaha blind semantics.
- [ ] [src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs): validate/apply Omaha blind values in DC.
- [ ] [src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs): verify Omaha continuous-play transitions.
- [ ] [src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs](src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs): register Omaha evaluator.
- [ ] [src/CardGames.Poker/Evaluation/Evaluators/HoldemHandEvaluator.cs](src/CardGames.Poker/Evaluation/Evaluators/HoldemHandEvaluator.cs): keep unchanged for Hold’Em.
- [ ] [src/CardGames.Poker/Games/Omaha/OmahaGame.cs](src/CardGames.Poker/Games/Omaha/OmahaGame.cs): preserve domain behavior.
- [ ] [src/CardGames.Poker/Games/Omaha/OmahaRules.cs](src/CardGames.Poker/Games/Omaha/OmahaRules.cs): ensure phase/model consistency with flow.
- [ ] [src/CardGames.Poker/Games/HoldEm/HoldEmGame.cs](src/CardGames.Poker/Games/HoldEm/HoldEmGame.cs): regression baseline.
- [ ] [src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs](src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs): regression baseline.

### Testing checklist
- [ ] [src/Tests/CardGames.Poker.Tests/Hands/OmahaHandTests.cs](src/Tests/CardGames.Poker.Tests/Hands/OmahaHandTests.cs): retain + extend evaluator-facing scenarios.
- [ ] [src/Tests/CardGames.Poker.Tests/Games/OmahaGameTests.cs](src/Tests/CardGames.Poker.Tests/Games/OmahaGameTests.cs): retain + update for phase/flow adjustments.
- [ ] [src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs](src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs): run as no-regression suite.
- [ ] [src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs): add Omaha cases.
- [ ] [src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs): add Omaha cases.
- [ ] [src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs](src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs): add Omaha DC lifecycle cases.

---

## 5) Backward Compatibility Strategy

- Keep all existing game codes, endpoints, and contracts functional.
- Add Omaha behavior as additive branches; avoid changing Hold’Em semantics.
- Avoid changing fallback behavior globally except where fallback currently misroutes Omaha to Five Card Draw.
- Preserve Dealer’s Choice behavior for all existing variants; only expand blind-based game classification to include Omaha.
- Run Hold’Em lifecycle integration tests as required release gate.

---

## 6) Acceptance Criteria

1. Omaha appears as selectable in Create Table and can be created with blinds.
2. Omaha appears in Dealer’s Choice and prompts for small/big blind (not ante/min-bet-only path).
3. Starting an Omaha hand routes through Omaha-compatible API path and deals 4 hole cards/player.
4. Omaha betting actions do not route to Five Card Draw fallback.
5. Omaha showdown enforces exactly 2 hole + 3 board cards.
6. Continuous play advances Omaha hands without breaking Dealer’s Choice rotation logic.
7. Existing Hold’Em and non-Omaha variants pass current integration tests unchanged.
8. No schema migration is required unless a newly discovered deployment-specific gap appears.

---

## 7) Test Strategy and Rollout Phases

### Test strategy
- **Unit:** Omaha evaluator/rules/flow behavior; branch predicates in UI helpers.
- **Integration:** create-game, choose-dealer-game, continuous-play Omaha paths; Hold’Em regression.
- **UI/manual:** Create Table selection, Dealer’s Choice selection, Table Play blind badges and start/showdown behavior.

### Rollout phases
- **Phase 0 (Hardening):** implement Omaha routing/evaluator/branch changes behind low-risk incremental commits.
- **Phase 1 (Internal validation):** run targeted Omaha + required regression suites.
- **Phase 2 (Staged deploy):** controlled non-prod enablement with explicit go/no-go gates.
	- **Execution checklist**
		- [ ] **Non-prod enablement**
			- [ ] Deploy Omaha code/contract updates to non-prod only.
			- [ ] Enable Omaha in Create Table + Dealer’s Choice behind non-prod-scoped configuration.
			- [ ] Confirm existing Hold’Em and Five Card Draw table creation/play paths remain available.
		- [ ] **Smoke checks (non-prod)**
			- [ ] Create Table: Omaha is selectable, blind inputs are shown, and blind values persist on create.
			- [ ] Dealer’s Choice: Omaha selection prompts blinds (not ante/min-bet-only inputs).
			- [ ] Table Play: Omaha hand starts successfully and deals 4 hole cards per player.
			- [ ] Action/showdown routing stays Omaha-compatible and does not fall back to Five Card Draw.
			- [ ] Showdown completes with Omaha rule enforcement (exactly 2 hole + 3 board) and hand closure.
		- [ ] **Rollback gates (immediate disable in non-prod)**
			- [ ] Omaha hand cannot start or complete end-to-end.
			- [ ] Any Omaha action/showdown path misroutes to Five Card Draw behavior.
			- [ ] Regression is observed in required Hold’Em baseline flows.
			- [ ] Contract/UI mismatch blocks normal table play.
		- [ ] **Promotion criteria (Phase 2 -> Phase 3)**
			- [ ] All non-prod Omaha smoke checks pass for Create Table and Dealer’s Choice flows.
			- [ ] No rollback gate is triggered during staged validation.
			- [ ] Required Omaha tests plus Hold’Em regression suite are green.
			- [ ] Lead sign-off recorded for production enablement.
- **Phase 3 (Prod release):** enable Omaha in Create Table + Dealer’s Choice for all users.
	- **Execution checklist**
		- [ ] **Production enablement**
			- [ ] Deploy Omaha code/contract updates to production.
			- [ ] Enable Omaha in production by setting `GameAvailability:EnableOmaha` to `true`.
		- [ ] **Smoke checks (prod)**
			- [ ] Create Table: Omaha is selectable, blind inputs are shown, and blind values persist on create.
			- [ ] Dealer’s Choice: Omaha selection prompts blinds (not ante/min-bet-only inputs).
			- [ ] Table Play: Omaha hand starts successfully and deals 4 hole cards per player.
			- [ ] Action/showdown routing stays Omaha-compatible and does not fall back to Five Card Draw.
			- [ ] Showdown completes with Omaha rule enforcement (exactly 2 hole + 3 board) and hand closure.
		- [ ] **Monitoring**
			- [ ] Monitor API logs/traces for 4xx/5xx spikes and Omaha-specific errors immediately post-enable.
			- [ ] Watch latency/error-rate dashboards for regressions in Create Table, Dealer’s Choice, and Table Play flows.
		- [ ] **Rollback (flip config back)**
			- [ ] Disable Omaha by setting `GameAvailability:EnableOmaha` to `false` and redeploy/restart if required.
			- [ ] Confirm Omaha disappears from available-games and cannot be created/selected in Dealer’s Choice.

---

## 8) Risks / Open Questions

- Generic showdown currently has Omaha-risk behavior (positional hand creation path truncates to 2 hole cards and is not explicitly Omaha-aware). Confirm whether Omaha uses dedicated endpoint path or generic showdown path as source of truth.
- Web currently has no Omaha contract/API registration; deciding between Omaha-specific endpoints vs expanded generic endpoints affects contract regeneration scope.
- Omaha phase model consistency needs confirmation (`OmahaFlowHandler` initial phase vs `OmahaRules` phase list).
- Metadata/rules max-player values for Omaha are inconsistent across files (attribute/rules/property/test seed). Decide single canonical value before release.
- Blind validation policy for blind-based variants in Dealer’s Choice should be explicit (required vs optional at command boundary).