# Follow the Queen PRD (Product Requirements Document)

## 1. Purpose
Add the **Follow the Queen** poker variant as a fully playable game in the existing CardGames system, with complete end-to-end support across API, background services, game flow, UI, and state rendering. This PRD provides exhaustive implementation guidance for an LLM developer to integrate the game into the current architecture and conventions.

## 2. Scope
### In Scope
- Full UI support for Follow the Queen in the Blazor client.
- API endpoints and command/query handlers needed for a playable game.
- Game flow integration (phases, dealing, betting, showdown).
- Table state rendering (public/private state) including wild card rules.
- Background processing via existing game loop.
- Game API routing and DI registration.
- Game type registration and metadata discovery if needed.
- Consistent handling of game phases and special rules.

### Out of Scope
- Non-poker game framework changes.
- Major architecture redesign (e.g., ICardGame abstraction).
- Rewriting background services or table state builder for generic handling beyond required changes.

## 3. Current State
- `FollowTheQueenGame` exists and defines gameplay rules and phases, but the game is not wired into the API/UI.
- `PokerGameMetadataRegistry` and `PokerGameRulesRegistry` are reflection-based in newer code; but game type constants and fallbacks still exist.
- Several UI and API components still branch by game type code or phase lists.
- `TableStateBuilder` and `ContinuousPlayBackgroundService` contain hardcoded checks.
- `TablePlay.razor` does not handle Follow the Queen phases or special rule display.

## 4. Goals and Success Criteria
### Goals
- Follow the Queen is selectable and playable in the UI.
- Players can start a hand, bet through all streets (Third–Seventh), and complete showdown.
- Wild card rules (Queens + “following” rank) are displayed in UI and API responses.
- Phases and card visibility are correct for all players.
- Background automation works with Follow the Queen’s phase flow.

### Success Criteria
- The game appears in available games list with correct metadata.
- A hand can be completed without errors in automated loop or manual UI flow.
- Table state shows correct cards and hand descriptions for all phases.
- No fallback to Five Card Draw handlers or UI flows when Follow the Queen is active.

## 5. User Stories
1. **As a player**, I can choose Follow the Queen from the available games list.
2. **As a player**, I can start and complete a hand in Follow the Queen using the UI.
3. **As a player**, I can see my own hole cards and others’ visible cards correctly.
4. **As a player**, I can see the wild card rule for the current hand.
5. **As a player**, I can view showdown results and hand descriptions.

## 6. Functional Requirements

### 6.1 Game Registration & Metadata
- The game must be discoverable from existing metadata registry (reflection).
- If constants are used for comparisons, define a `FollowTheQueenCode` constant (e.g., `FOLLOWTHEQUEEN`) in the registry or a shared constants file.
- Ensure the game appears in the “Available Poker Games” endpoint and UI list.

**Required updates**
- Ensure `FollowTheQueenGame` is discovered by `PokerGameMetadataRegistry` and `PokerGameRulesRegistry`.
- If there is a centralized game code constants file, add `FOLLOWTHEQUEEN` to avoid string literals.

### 6.2 Game API Routing
- The UI uses `GameApiRouter` and specific per-game API client wrappers.
- Follow the Queen requires its own wrapper or routing to generic endpoints.

**Required updates**
- Add `FollowTheQueenApiClientWrapper` implementing `IGameApiClient` in `CardGames.Poker.Web/Services/GameApi/`.
- Register wrapper in DI and map it in `GameApiRouter` by game type code.
- Ensure the wrapper points to the Follow the Queen API endpoints.

### 6.3 API Endpoints
Follow the Queen should expose endpoints matching existing game-specific endpoint patterns.

**Required new endpoints (API layer)**
Create `CardGames.Poker.Api/Features/Games/FollowTheQueen/` with standard handlers:
- `Razz`-style or stud-based equivalents (use Seven Card Stud patterns):
  - `StartHand`
  - `CollectAntes`
  - `DealHands` (Third Street)
  - `ProcessBettingAction`
  - `DealStreetCard` for Fourth–Seventh
  - `PerformShowdown`
  - `GetCurrentPlayerTurn`

**Endpoint mapping**
- Add Follow the Queen endpoint group registration in `MapFeatureEndpoints.cs` or ensure discovery if the mapping is reflection-based.

### 6.4 Game Flow Handler
The API game flow uses handlers per game type.

**Required updates**
- Add a `FollowTheQueenFlowHandler` in `CardGames.Poker.Api/GameFlow/`.
- Register it in `GameFlowHandlerFactory` keyed to `FOLLOWTHEQUEEN`.
- Ensure it returns correct phase transitions:
  - `WaitingToStart -> CollectingAntes -> ThirdStreet -> FourthStreet -> FifthStreet -> SixthStreet -> SeventhStreet -> Showdown -> Complete`.
- Ensure `Deal` behavior matches Follow the Queen rules:
  - Third Street: 2 hole + 1 board card.
  - Fourth–Sixth: 1 board card each.
  - Seventh: 1 hole card.

### 6.5 Background Service (Continuous Play)
The background service must not fall back to Five Card Draw behavior.

**Required updates**
- Add handling for Follow the Queen phases in `ContinuousPlayBackgroundService`:
  - Include `ThirdStreet`–`SeventhStreet` in “in-progress” phase set if missing.
  - Ensure dealing flow uses `DealSevenCardStudHandsAsync` equivalent or new Follow the Queen dealing method.
  - Ensure `CollectAntes` transitions to `ThirdStreet` and not to `Dealing` or `FirstBettingRound`.
- Ensure showdown and phase transitions do not include Kings and Lows-specific logic for Follow the Queen.
- Ensure auto action handling for betting uses phase categories or includes Third–Seventh streets.

### 6.6 Table State Builder
Follow the Queen must render correct private and public state.

**Required updates**
- Hand evaluation:
  - Add a Follow the Queen hand type/evaluator in domain layer if not present (`FollowTheQueenHand`).
  - In `TableStateBuilder`, map Follow the Queen to that hand evaluator.
- Card visibility:
  - During Third–Seventh streets, only face-up cards are public.
  - Player private state includes all hole cards.
- Showdown:
  - Add a Follow the Queen hand type in showdown evaluation if needed.
- Wild card rules:
  - The game has dynamic wilds (Queens + following rank).
  - `BuildWildCardRulesDto` should include a Follow the Queen rule using game state (face-up card order).

### 6.7 Auto Action Service
- Ensure betting phases list includes Third–Seventh streets.
- Ensure draw or special phases do not interfere.

### 6.8 Blazor UI: `TablePlay.razor`
- Ensure follow-the-queen phases are treated as betting phases.
- Add a helper boolean for `IsFollowTheQueen` if game-specific UI logic is necessary.
- Display dynamic wild card info in the table UI (banner or rule text).
- Ensure card layout (stud-style) shows face-up board cards correctly.
- Ensure available actions for Third–Seventh streets are enabled.

### 6.9 Phase Descriptions
- Ensure `PhaseDescriptionResolver` can resolve all Follow the Queen phases (Third–Seventh).
- Ensure `Phases.cs` already includes these values; if not, add them.

## 7. Non-Functional Requirements
- Must be consistent with existing naming conventions and folder structure.
- Should minimize new branching on game type where possible (use game rules metadata).
- Must not break existing games (Five Card Draw, Seven Card Stud, etc.).

## 8. Detailed Implementation Checklist

### 8.1 Domain Layer
1. Ensure `FollowTheQueenHand` exists under `CardGames.Poker/Hands/StudHands/` or create it.
2. Ensure `FollowTheQueenGamePlayer` exists and contains required card collections.
3. If not already defined, ensure the game exposes method to identify the current “following rank” for wild cards.

### 8.2 API Layer
1. Create `CardGames.Poker.Api/Features/Games/FollowTheQueen/FollowTheQueenApiMapGroup.cs`.
2. Add `v1/Commands/` for:
   - `StartHand`
   - `CollectAntes`
   - `DealHands` (Third Street)
   - `DealStreetCard` (Fourth–Seventh)
   - `ProcessBettingAction`
   - `PerformShowdown`
3. Add `v1/Queries/GetCurrentPlayerTurn`.
4. Update `MapFeatureEndpoints` to include Follow the Queen endpoints (unless using discovery).

### 8.3 Game Flow
1. Add `FollowTheQueenFlowHandler`.
2. Update `GameFlowHandlerFactory` to map `FOLLOWTHEQUEEN` to the new handler.
3. Ensure phase transitions align with game rules and `FollowTheQueenGame`.

### 8.4 Background Service
1. Add Follow the Queen phase handling in `ContinuousPlayBackgroundService`.
2. Ensure dealing uses stud-style logic and does not call draw-related logic.
3. Ensure showdown uses Follow the Queen hand evaluation.

### 8.5 Table State Builder
1. Update `BuildPrivateStateAsync` to evaluate Follow the Queen hands.
2. Update `BuildShowdownPublicDtoAsync` to include Follow the Queen.
3. Update `BuildWildCardRulesDto` to include dynamic wild rules.
4. Ensure card ordering is stud-appropriate where necessary.

### 8.6 Auto Action Service
1. Add Third–Seventh streets to betting phase lists if they are still hardcoded.

### 8.7 Web UI
1. Add `FollowTheQueenApiClientWrapper` (or a generic wrapper) and register in DI.
2. Update `GameApiRouter` mapping.
3. Update `TablePlay.razor`:
   - Recognize Follow the Queen phase names.
   - Display dynamic wild card rules.
   - Ensure stud card visibility in player and opponent areas.

## 9. Data Contracts and DTOs
- Ensure any game-specific DTOs or response shapes match existing patterns for stud games.
- Wild card DTO should include:
  - `IsDynamic` (bool)
  - `WildRanks` (list)
  - `Description` (string)

## 10. Testing Requirements

### 10.1 Unit Tests
- Follow the Queen rules should validate dealing and phase progression.
- Wild card logic should produce correct wild rank based on face-up Queen sequence.

### 10.2 Integration Tests
- Start a hand and progress through all streets in the API.
- Ensure correct game state after each phase.

### 10.3 UI Tests (Manual or Automated)
- Verify correct actions are enabled on Third–Seventh streets.
- Verify proper card visibility across players.
- Verify wild card display updates after face-up Queens.

## 11. Risks and Mitigations
- **Risk:** Stud-specific logic may be missing in background service and table state builder.
  - **Mitigation:** Reuse Seven Card Stud patterns and extend where needed.
- **Risk:** UI may not support dynamic wild cards.
  - **Mitigation:** Add explicit UI section for wild card rules from DTO.

## 12. Deliverables
- Fully integrated Follow the Queen game playable via UI.
- Updated API endpoints and routing.
- Updated state builder and background service logic.
- Updated UI routing and rendering.
- Documentation in this PRD.

## 13. Acceptance Criteria
- Follow the Queen appears and is selectable in the UI.
- Hand completes successfully with correct phase progression.
- Wild card rules are displayed and accurate.
- No fallback to Five Card Draw handlers for Follow the Queen.
- No regressions to existing games.
