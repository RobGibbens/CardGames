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
- `FollowTheQueenGame` exists in `src\CardGames.Poker\Games\FollowTheQueen\FollowTheQueenGame.cs`. It defines gameplay rules and phases but is not fully wired into the API/UI.
- `PokerGameMetadataRegistry` (`CardGames.Poker.Api\Games\PokerGameMetadataRegistry.cs`) already contains the constant `public const string FollowTheQueenCode = "FOLLOWTHEQUEEN";` and uses reflection for discovery.
- `Phases` enum (`CardGames.Poker\Betting\Phases.cs`) already contains `ThirdStreet`, `FourthStreet`, `FifthStreet`, `SixthStreet`, `SeventhStreet`.
- `FollowTheQueenHand` exists at `CardGames.Poker\Hands\StudHands\FollowTheQueenHand.cs`.
- Several UI and API components still branch by game type code or phase lists.
- `TableStateBuilder` and `ContinuousPlayBackgroundService` contain hardcoded checks for Stud/Baseball games but lack checks for Follow the Queen.
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
- Ensure the game appears in the “Available Poker Games” endpoint and UI list.
- Use `PokerGameMetadataRegistry.FollowTheQueenCode` for any code comparisons.

**Required updates**
- None expected for registration (reflection handles it), but verify `FollowTheQueenGame` has the correct `[PokerGameMetadata]` attribute matching the `FollowTheQueenCode`.

### 6.2 Game API Routing
- The UI uses `GameApiRouter` and specific per-game API client wrappers.
- Follow the Queen requires its own wrapper or routing to generic endpoints.

**Required updates**
- Create `FollowTheQueenApiClientWrapper.cs` in `CardGames.Poker.Web\Services\GameApi\` implementing `IGameApiClient`.
- Register the wrapper in DI (`Program.cs` or `ServiceCollectionExtensions.cs`).
- Map the wrapper in `GameApiRouter` (`CardGames.Poker.Web\Services\GameApi\GameApiRouter.cs`) by `FOLLOWTHEQUEEN` game type code.

### 6.3 API Endpoints
Follow the Queen should expose endpoints matching existing game-specific endpoint patterns (similar to Seven Card Stud).

**Required new file structure (API layer)**
Create folder `CardGames.Poker.Api\Features\Games\FollowTheQueen\` mimicking `SevenCardStud`:
- `FollowTheQueenApiMapGroup.cs`: Registers the group (e.g., `/api/games/followthequeen`).
- `v1\Commands\StartHand\StartFollowTheQueenHandEndpoint.cs`
- `v1\Commands\CollectAntes\CollectFollowTheQueenAntesEndpoint.cs`
- `v1\Commands\DealHands\DealFollowTheQueenHandsEndpoint.cs` (Third Street)
- `v1\Commands\DealStreetCard\DealFollowTheQueenStreetCardEndpoint.cs` (Reusable for 4th-7th streets)
- `v1\Commands\ProcessBettingAction\ProcessFollowTheQueenBettingActionEndpoint.cs`
- `v1\Commands\PerformShowdown\PerformFollowTheQueenShowdownEndpoint.cs`
- `v1\Queries\GetCurrentPlayerTurn\GetFollowTheQueenCurrentPlayerTurnEndpoint.cs`

**Endpoint mapping**
- Update `CardGames.Poker.Api\Features\MapFeatureEndpoints.cs` to call `app.AddFollowTheQueenEndpoints()`.

### 6.4 Game Flow Handler
The API game flow uses handlers per game type.

**Required updates**
- Create `FollowTheQueenFlowHandler.cs` in `CardGames.Poker.Api\GameFlow\`. It should implement `IGameFlowHandler` (or inherit from a Stud base if available).
- Register it in `GameFlowHandlerFactory.cs` (`CardGames.Poker.Api\GameFlow\GameFlowHandlerFactory.cs`) keyed to `FOLLOWTHEQUEEN`.
- Ensure it properly sequences:
  - `WaitingToStart -> CollectingAntes -> ThirdStreet -> FourthStreet -> FifthStreet -> SixthStreet -> SeventhStreet -> Showdown -> Complete`.
- Ensure `Deal` methods on the handler delegate to the correct `FollowTheQueenGame` methods (`DealThirdStreet`, `DealStreetCard`).

### 6.5 Background Service (Continuous Play)
The background service (`CardGames.Poker.Api\Services\ContinuousPlayBackgroundService.cs`) must not fall back to Five Card Draw behavior. It currently has hardcoded checks for `SevenCardStud` and `KingsAndLows`.

**Required updates**
- `DealHandsAsync`: Add check for `isFollowTheQueen` and call `DealFollowTheQueenHandsAsync` (which basically calls `game.DealThirdStreet()`).
- `PerformShowdownAsync`: Add check for `isFollowTheQueen` and call `PerformFollowTheQueenShowdownAsync`.
- `ProcessGameAsync`: Ensure phase transitions for `ThirdStreet` through `SeventhStreet` are handled correctly (or generalized to use the `GameFlowHandler` if possible, but minimal changes require adding the specific checks).
- **CRITICAL**: Ensure `CollectAntes` transitions to `ThirdStreet`, NOT `Dealing`.

### 6.6 Table State Builder
`CardGames.Poker.Api\Services\TableStateBuilder.cs` needs specific updates to correctly render Stud-like state for Follow the Queen.

**Required updates**
- `BuildPrivateStateAsync`:
  - Allow `FollowTheQueenCode` to enter the "Seven Card Stud / Baseball" logic block (handling hole/board cards).
  - Use `FollowTheQueenHand` for hand evaluation description in private state.
- `BuildShowdownPublicDtoAsync`:
  - Add `FollowTheQueenCode` check.
  - Instantiate `FollowTheQueenHand` for final showdown evaluation and card display.
- `BuildWildCardRulesDto` (Line ~1404):
  - Add logic to extract the "Following Rank" from the `FollowTheQueenGame` state.
  - Return a `WildCardRulesDto` describing that Queens are wild AND the specific following rank is wild.

### 6.7 Auto Action Service
`CardGames.Poker.Api\Services\AutoActionService.cs`
- Ensure `BettingPhases` hash set includes `ThirdStreet`, `FourthStreet`, `FifthStreet`, `SixthStreet`, `SeventhStreet`. (Likely already done for Stud, but verify).

### 6.8 Blazor UI: `TablePlay.razor`
`CardGames.Poker.Web\Components\Pages\TablePlay.razor`

**Required updates**
- Add `private bool IsFollowTheQueen => string.Equals(_gameTypeCode, "FOLLOWTHEQUEEN", StringComparison.OrdinalIgnoreCase);`
- Update card rendering areas that check `IsSevenCardStud` to also include `IsFollowTheQueen` (so board cards are shown face up, hole cards face down).
- Ensure "Street" labels are displayed correctly (Third Street, etc.).
- Add a **Wild Card Banner** component or section that displays: "Queens are Wild" + dynamic "Following Rank: [Rank] is Wild" if applicable.

### 6.9 Phase Descriptions
- Ensure `PhaseDescriptionResolver` can resolve all Follow the Queen phases (Third–Seventh).

## 7. Non-Functional Requirements
- Must be consistent with existing naming conventions and folder structure.
- Should minimize new branching on game type where possible (use game rules metadata).
- Must not break existing games (Five Card Draw, Seven Card Stud, etc.).

## 8. Detailed Implementation Checklist

### 8.1 Domain Layer
1. Verify `FollowTheQueenHand` is fully functional.
2. Ensure `FollowTheQueenGame` exposes `GetCurrentFollowingWildRank()` (it does).

### 8.2 API Layer
1. Create `FollowTheQueenApiMapGroup.cs` and all command/query endpoints.
2. Update `MapFeatureEndpoints.cs`.

### 8.3 Game Flow
1. Create `FollowTheQueenFlowHandler.cs`.
2. Register in `GameFlowHandlerFactory.cs`.

### 8.4 Background Service
1. Update `ContinuousPlayBackgroundService.cs` to handle `FOLLOWTHEQUEEN` logic for dealing (Third Street) and Showdown.
2. Ensure betting rounds for streets are picked up.

### 8.5 Table State Builder
1. Update `BuildPrivateStateAsync` to include `FollowTheQueenCode` in the Stud logic block.
2. Update `BuildShowdownPublicDtoAsync` to handle FTQ hand evaluation.
3. Update `BuildWildCardRulesDto` to include dynamic FTQ wild info.

### 8.6 Web UI
1. Create `FollowTheQueenApiClientWrapper.cs`.
2. Register in `GameApiRouter`.
3. Update `TablePlay.razor` to support FTQ rendering (stud layout) and Wild Card rules display.

## 9. Data Contracts and DTOs
- `WildCardRulesDto`: Ensure it can transport the dynamic wild rank (might need a `WildRanks` list or `Description` field).

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
  - **Mitigation:** Copy/adapt Seven Card Stud patterns explicitly.
- **Risk:** UI may not support dynamic wild cards.
  - **Mitigation:** Add specific UI logic in `TablePlay.razor` that reads `WildCardRulesDto` and displays it prominently.

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
