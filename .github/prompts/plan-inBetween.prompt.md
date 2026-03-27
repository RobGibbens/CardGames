# In-Between (INBETWEEN) - Final Execution-Ready Implementation Plan

## Mode
Production-Ready

## Core Decisions (Locked)
- In-Between is a single continuous game flow (not traditional poker hand/showdown lifecycle).
- There is no traditional showdown. The game ends when a player empties the pot via a winning bet.
- Zero-chip players are not removed from turn order. They are forced to pass on every turn while at 0 chips.
- New players cannot join once an In-Between game has started.
- On non-Dealer's Choice tables, game completion ends the game.
- On Dealer's Choice tables, completion transitions to WaitingForDealerChoice for the next dealer seat.

## Phase 1: Discovery and Mapping (Complete before edits)
1. Inspect uncommitted changes and preserve intent.
   - `git status`
   - `git diff --name-only`
   - `git diff`
2. Confirm all touchpoints:
   - Domain: `src/CardGames.Poker`
   - API: `src/CardGames.Poker.Api`
   - Web: `src/CardGames.Poker.Web`
   - Contracts: `src/CardGames.Contracts`
   - Tests: `src/Tests`
   - Docs: `docs`
3. Validate nearest analogs and extension seams:
   - Screw Your Neighbor flow/deck/turn logic
   - Join-game late-join guard
   - Dealer's Choice terminal transition in background service
   - Web start-flow and result-phase assumptions

## Phase 2: Implementation

### A. Domain Layer
1. Add game metadata and rules:
   - `src/CardGames.Poker/Games/InBetween/InBetweenGame.cs`
   - `src/CardGames.Poker/Games/InBetween/InBetweenRules.cs`
2. Metadata:
   - Game Name: In-Between
   - Game Type Code: INBETWEEN
   - Image: inbetween.png
   - Variant family: Custom (`VariantType.Other`)
   - Players: 2-20
3. Rules model must reflect:
   - Ante-funded initial pot
   - Per-turn boundary reveal and bet/pass
   - Ace choice gate when first boundary card is Ace
   - Strictly-between win logic
   - POST logic (match boundary rank => lose 2x bet to pot)
   - First-orbit full-pot restriction
   - Continuous deck with refresh at <= 3 cards remaining
   - End condition: pot reaches zero (no showdown)
4. Add any needed phase constant for In-Between turn lifecycle in:
   - `src/CardGames.Poker/Betting/Phases.cs`

### B. API Layer

#### B1. Registration and Flow
1. Add game code constant in:
   - `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`
2. Add flow handler:
   - `src/CardGames.Poker.Api/GameFlow/InBetweenFlowHandler.cs`
3. Flow handler responsibilities:
   - Initialize first actor (left of dealer)
   - Manage per-turn phases (ace-choice gate -> bet/pass -> resolution -> next actor)
   - Enforce forced-pass for zero-chip actors
   - Refresh deck before turn when deck has <=3 cards
   - Emit deck-refresh signal for UI toast
   - Determine game completion when pot == 0
   - No traditional showdown transition

#### B2. Commands/Endpoints
1. Add game-specific endpoint map group and V1 routes:
   - `src/CardGames.Poker.Api/Features/Games/InBetween/InBetweenApiMapGroup.cs`
   - `src/CardGames.Poker.Api/Features/Games/InBetween/v1/V1.cs`
2. Add commands and handlers for:
   - Ace high/low choice (only when first boundary card is Ace)
   - Bet/pass action (pass = 0)
3. Validation safeguards (server-side):
   - Not player turn
   - Betting before second boundary exists
   - Second boundary before ace choice when required
   - Bet < 0
   - Bet > pot
   - Bet > player chips
   - Illegal full-pot bet during first orbit
   - Invalid state transitions

#### B3. State Projection / Reconnect
1. Add In-Between state projection to:
   - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
2. Project enough state for reconnect and spectators:
   - Current actor
   - Dealer
   - Boundary cards
   - Pending ace choice
   - Current legal bet range
   - Forced-pass state (zero chips)
   - Current turn outcome (Win/Lose/POST)
   - Pot amount
   - Deck refresh flag/marker
   - First-orbit completion tracking
3. Ensure persistence fields/variant state survive reloads exactly.

#### B4. Join Restriction
1. Extend existing late-join guard in:
   - `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/JoinGame/JoinGameCommandHandler.cs`
2. Rule:
   - For INBETWEEN, reject joins after game start (same pattern as SYN).

#### B5. Dealer's Choice Completion
1. Add terminal In-Between transition in:
   - `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`
2. Behavior:
   - Non-DC table: remain terminal complete.
   - DC table: transition to `WaitingForDealerChoice` and advance DC dealer seat.
3. Reuse existing DC transition helper patterns.

#### B6. Action Feed / History
1. Ensure player action notifications include In-Between semantic actions:
   - Ace low/high selected
   - Pass
   - Bet amount
   - Win/Lose/POST with amounts
2. Ensure hand/history logging records the game-completion event and pot-empty winner reason.

### C. Contracts Layer
1. Add In-Between API interface and DTOs under `src/CardGames.Contracts`.
2. Include request/response shapes for ace choice and bet/pass actions.
3. Keep generated contract file rules intact:
   - Do not manually edit `src/CardGames.Contracts/RefitInterface.v1.cs`.
   - Regenerate only if contract-generation workflow requires it.

### D. Web Layer

#### D1. API Routing
1. Add In-Between route mappings in:
   - `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`
   - router implementation file(s)
2. Include methods for ace choice and bet/pass.

#### D2. Setup/Selection Surfaces
1. Ensure INBETWEEN appears correctly in:
   - Create table
   - Edit table
   - Dealer Choice modal
   - Table canvas game labels/cards
2. Use ante-based setup as required by In-Between rules.
3. Add image asset:
   - `src/CardGames.Poker.Web/wwwroot/images/games/inbetween.png`

#### D3. TablePlay Integration
1. Add game predicate flags and phase checks in:
   - `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
2. Add In-Between turn UI:
   - Boundary cards
   - Ace choice prompt when required
   - Bet controls with legal range
   - Pass action
   - Turn result (win/lose/POST)
   - Deck refresh toast
3. Suppress poker-specific constructs for In-Between:
   - No traditional showdown expectation
   - No draw-panel behavior for this game
   - No hand-odds/hand-eval dependencies for player-held cards
4. Add client-side join guard for started In-Between tables (matching API guard).
5. Update StartGame flow branch to invoke the right endpoints for In-Between.
6. Ensure result/completion path does not rely on `TryLoadShowdownAsync` for In-Between.

## Phase 3: Testing

### A. Integration Tests (Required)
Add focused tests under `src/Tests/CardGames.IntegrationTests/Games/InBetween/`:
1. Lifecycle happy path:
   - Start
   - Antes applied
   - Turn progression
   - Ace-choice branch
   - Bet resolution
   - Pot reaches zero
   - Terminal completion (no showdown)
2. Negative path:
   - Action out of turn
   - Illegal bet amount
   - Full-pot during first orbit
   - Missing ace choice when required
3. Regression path:
   - Join disallowed after start
   - Zero-chip forced-pass behavior
   - DC completion transitions to WaitingForDealerChoice

### B. Web/Router Tests
1. Add router mapping tests for In-Between API routing.
2. Add any UI logic tests feasible for new game predicate/branching behavior.

## Phase 4: Verification
Run from repo root in this order:
1. `dotnet build src/CardGames.sln`
2. `dotnet test src/CardGames.sln`
3. Targeted tests:
   - `dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter FullyQualifiedName~InBetween`
4. `dotnet run --project src/CardGames.Poker.Api`
5. `dotnet run --project src/CardGames.Poker.Web`

If any command fails:
- Fix failures introduced by this change.
- Re-run affected commands.
- Report exact failure and remediation.

## Phase 5: Docs Updates
1. Update game-addition docs with In-Between mapping and any reusable pattern introduced.
2. Document explicitly:
   - No-showdown completion model
   - Pot-empty terminal rule
   - Zero-chip forced-pass semantics
   - Late-join restriction
   - DC handoff behavior

## Acceptance Checklist
- INBETWEEN is selectable and fully wired through registration, routing, and UI.
- Game state persists and rehydrates correctly mid-turn.
- All rule validations are server-enforced.
- No traditional showdown is required or triggered for completion.
- Winner is determined by emptying pot through bet resolution.
- Zero-chip players remain in rotation and always pass.
- New joins are blocked after game start.
- Dealer's Choice completion returns to WaitingForDealerChoice.
- Mandatory build/tests/run commands executed and reported.

## Risks and Guardrails
- Do not modify generated contract files directly.
- Keep changes localized; avoid broad architecture refactors.
- Preserve unrelated working tree changes.
- Do not stage/commit/reset/discard changes.

## Final Reporting Format (for implementation run)
1. Summary
2. Per-file checklist (path, change type, purpose, acceptance status)
3. Verification results (command-by-command)
4. Targeted test coverage
5. Pitfalls/watchouts
6. Follow-up recommendations
