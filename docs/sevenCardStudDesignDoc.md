# Seven Card Stud – Design Document (End-to-End)

> Goal: Add **Seven Card Stud** as a playable poker variant across the solution (contracts → API → core/poker engine → web UI), including correct dealing streets, bring-in  (ante), betting flow, and client rendering of **hole cards** (visible only to the owning player).

This document is intentionally precise and implementation-oriented so another LLM can implement with minimal guessing.

## Important: reuse existing Seven Card Stud implementation (do not rewrite)

Most of the Seven Card Stud game logic already exists in the repo. The implementation work described in this document should prioritize **wiring/integration**, **DTO visibility plumbing**, **API Features**, **SignalR messaging**, and **UI rendering**, and should avoid duplicating logic that is already implemented.

Primary existing implementation files (treat these as the source of truth):
- `CardGames.Poker/Hands/StudHands/SevenCardStudHand.cs` – hand model / card organization for stud.
- `CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs` – core game loop: phases/streets, dealing, betting flow, bring-in  (ante), showdown, etc.
- `CardGames.Poker/Games/SevenCardStud/SevenCardStudGamePlayer.cs` – per-player stud state/behavior used by the game.
- `CardGames.Poker/Simulations/Stud/SevenCardStudSimulation.cs` – simulation harness; useful for validating dealing/betting progression.
- `CardGames.Poker.CLI/Play/SevenCardStudPlayCommand.cs` – a working CLI integration that can be used as a reference for how to drive the game via actions and for expected state transitions.

Design principle for the implementation:
- **Reuse** `SevenCardStudGame` and related classes as-is wherever possible.
- If behavior differs from requirements, prefer **small, localized adjustments** inside these existing classes rather than creating parallel logic elsewhere.
- API/Web should call into the game engine; they should not re-implement stud rules.

---

## 0) Scope and non-goals

### In scope
- New game type: **Seven Card Stud** (high only) with standard rules:
  - Initial deal: 2 down (hole) + 1 up.
  - Betting: bring-in  (ante) forced by lowest upcard on 3rd street.
  - Streets 4th, 5th, 6th: deal 1 up each street, bet each street.
  - 7th street (river): deal 1 down, final betting, showdown.
  - No community cards.
- Server computes private/public visibility per card.
- Web UI renders:
  - Upcards for everyone.
  - Hole cards for seat owner only.
  - Card backs for opponents’ hole cards.
- Integration with existing table/game pipeline:
  - Game rules endpoint includes max cards = 7.
  - Action API supports Call/Check/Bet/Raise/Fold and bring-in  (ante).
- Contract updates required to carry visibility metadata.

### Non-goals (can be future work)
- Stud Hi/Lo, Razz.
- Antes configuration UI (but engine must support ante if existing table already supports it).
- Side pots / split pot edge cases beyond what existing betting engine already supports.

---

## 1) Repository orientation / key projects

- `CardGames.Poker` – poker game logic, evaluation, games.
- `CardGames.Poker.Api` – ASP.NET API used by Web and CLI, includes hubs and features.
- `CardGames.Contracts` – shared DTOs and Refit interfaces.
- `CardGames.Poker.Web` – Blazor web UI with components such as `TablePlay.razor`, `TableSeat.razor`.

---

## 2) Domain model & behavior specification

### 2.1 GameTypeCode
- Add `SEVENCARDSTUD` as a game type code.
- Ensure it appears wherever game type selection/listing exists (dashboard, create table, etc.) if applicable.

### 2.2 Stud phases (streets)
Define canonical phase identifiers (exact strings used server→client):

- `ThirdStreet` (initial deal + bring-in  (ante) determination)
- `FourthStreet`
- `FifthStreet`
- `SixthStreet`
- `SeventhStreet`
- `Showdown` (optional explicit phase)

Each phase should also carry a **PhaseCategory** used by the UI to decide whether to show draw/discard UI.

- For all Stud streets: `PhaseCategory = "Betting"`.
- There is **no phase** with `PhaseCategory = "Drawing"`.

This ensures the existing UI does not display draw controls.

Implementation note: `SevenCardStudGame.cs` already encodes street progression; align client-facing phase names with what it produces rather than inventing a second set.

### 2.3 Card visibility rules
Each player has a *private* view; table observers have a *public* view.

For Seven Card Stud:
- On ThirdStreet deal:
  - Card1 (down): visible to owner, hidden to others.
  - Card2 (down): visible to owner, hidden to others.
  - Card3 (up): visible to everyone.
- On Fourth/Fifth/Sixth:
  - New card (up): visible to everyone.
- On SeventhStreet:
  - New card (down): visible to owner, hidden to others.
- At showdown (or when hand ends):
  - Depending on current product behavior, either:
    - Reveal all cards for remaining players, or
    - Reveal only best 5 (not recommended).
  - Pick one consistent approach. Recommended: reveal all 7 for active players at showdown.

Visibility representation must not overload `IsFaceUp`. We need a separate flag:
- `IsPubliclyVisible`: if true, all players should see the actual face.
- `IsFaceUp`: physical orientation on table. For Stud upcards, true. For hole/downcards, false.

Owner should see the actual face for any card where `IsPubliclyVisible == true` OR (not publicly visible but the card belongs to the owner).

Integration note: do not re-derive which cards are up/down in the API/Web. Instead, propagate whatever `SevenCardStudHand` / `SevenCardStudGame` emits for up/down cards into DTOs plus the new `IsPubliclyVisible` flag.

---

## 3) Contracts changes (`CardGames.Contracts`)

### 3.1 Update CardInfo / PrivateStateDto
Locate card DTO(s) used by Web for rendering private state:
- Search target types: `CardInfo`, `PrivateStateDto`.

**Add property:**
- `bool IsPubliclyVisible { get; set; }`

Rules:
- For Five Card Draw, typically all player cards are not publicly visible during play. Set `IsPubliclyVisible=false` in private state, and use owner privilege to render.
- For Stud, set `IsPubliclyVisible=true` for upcards.

**Backwards compatibility:**
- If older servers don’t set it, default should behave like existing:
  - If missing, Web should treat `IsPubliclyVisible = IsFaceUp` OR default false. (Choose one; recommended default: `IsPubliclyVisible = IsFaceUp` to preserve prior semantics where face-up implied public.)

### 3.2 Add Seven Card Stud API Refit interface
In `CardGames.Contracts` there is a pattern:
- `IFiveCardDrawApiExtensions.cs`
- `RefitInterface.v1.cs`
- Possibly other game-specific interfaces.

Create a Refit interface:
- `ISevenCardStudApi`

Methods should match existing game style:
- `Task<ActionResponseDto> ActionAsync(Guid tableId, PlayerActionRequestDto request)`

If existing design splits draw actions (e.g., `DrawAsync`), Stud does **not** implement draw. Keep interface minimal.

Also add DI extension:
- `ISevenCardStudApiExtensions` similar to `IFiveCardDrawApiExtensions` if that exists.

Exact URL routes must match server controller endpoints.

---

## 4) Server API (`CardGames.Poker.Api`)

### 4.1 Endpoints
Add endpoints for Seven Card Stud actions.

Patterns in project:
- Use existing controllers/minimal APIs/features for other games.

Required endpoints:
- `POST /api/poker/sevencardstud/{tableId}/action`
  - Body: `PlayerActionRequestDto`
  - Response: `ActionResponseDto`

If the API uses a shared poker action endpoint already (not game-specific), then **do not** add a new endpoint. Instead:
- Ensure game routing allows `GameTypeCode=SEVENCARDSTUD` tables to be acted on.

Integration note: the API layer should call into the existing `SevenCardStudGame` implementation (and related types) and only map game state → DTOs; do not re-implement dealing, bring-in  (ante), or betting rules in controllers.

### 4.2 SignalR updates (if applicable)
If there are hub messages for private state updates, ensure stud’s private state contains correct card visibility metadata.

### 4.3 Migrations / persistence
If table config persists game type code, ensure `SEVENCARDSTUD` is allowed.

---

## 5) Poker engine / gameplay (`CardGames.Poker`)

### 5.1 New game implementation
A Seven Card Stud implementation already exists in:
- `CardGames.Poker/Games/SevenCardStud/SevenCardStudGame.cs`
- `CardGames.Poker/Games/SevenCardStud/SevenCardStudGamePlayer.cs`
- `CardGames.Poker/Hands/StudHands/SevenCardStudHand.cs`

Implementation work should focus on:
- Ensuring this game is reachable via the normal table/game factory routing.
- Ensuring state mapping includes visibility metadata needed by clients.

Avoid creating a second “stud game” module.

Key responsibilities (already largely implemented):
- Deal schedule by phase.
- Determine bring-in  (ante) player.
- Determine action order per street.
- Provide `TableState` updates with:
  - Current phase name (`ThirdStreet`, ...)
  - Phase category (`Betting`)
  - Per-seat card lists with orientation and public visibility.

### 5.2 bring-in  (ante)
Rules:
- ThirdStreet: forced bring-in  (ante) by lowest exposed upcard among active players.
- If ties by rank, break ties by suit order (define ordering explicitly). Common: clubs < diamonds < hearts < spades.

Implementation:
- After initial deal, compute bring-in  (ante) seat.
- Betting for ThirdStreet starts with bring-in  (ante) seat.

Action modeling:
- If existing action types include only `Bet`, represent bring-in  (ante) as a `Bet` with `MinAmount = BringInAmount` and `MaxAmount` per limits.
- If limits exist (fixed limit stud): define betting structure; otherwise treat as no-limit/pot-limit depending on platform.

### 5.3 Betting rounds
- Each street is a betting round.
- After betting completes:
  - Advance to next street.
  - Deal appropriate cards.
  - Determine first-to-act:
    - FourthStreet+: highest exposed hand acts first (by rank of exposed cards; specify algorithm).

Algorithm for first-to-act after ThirdStreet:
- Compare players’ exposed cards (upcards only) as poker hand values (best 5 of exposed, or special stud rule: highest upcard on 4th street; later streets use highest exposed hand). Choose one consistent rule. Standard:
  - 4th street: highest exposed hand (typically highest rank, pairs highest, etc.).

If existing engine already contains hand comparison utilities, reuse.

Implementation note: before changing bet-order logic, inspect `SevenCardStudGame.cs` to confirm what is already implemented and only adjust it if it is incorrect.

### 5.4 Showdown and hand evaluation
- Evaluate best 5-card hand from 7 cards.
- Use existing evaluator in `CardGames.Poker/Evaluation/`.

### 5.5 TableStateBuilder changes
Wherever card DTOs are mapped:
- Include both `IsFaceUp` and the new `IsPubliclyVisible`.
- For opponents’ hidden cards in public state, either:
  - Omit card rank/suit, include placeholder count, OR
  - Include card objects with `IsPubliclyVisible=false` and no face value.

Choose the approach that matches current system.

---

## 6) Web UI changes (`CardGames.Poker.Web`)

### 6.1 Replace hardcoded per-game API switching
Current plan notes `TablePlay.razor` injects multiple APIs and switches on game type.

Implement strategy pattern:

#### 6.1.1 New interface
Create `IGamePlayService` in Web project (or shared UI layer):
- `Task<ActionResponseDto> ActionAsync(Guid tableId, PlayerActionRequestDto request, CancellationToken ct)`
- `Task<DrawResponseDto?> DrawAsync(...)` (optional; can return null / throw NotSupported)
- `bool SupportsDraw { get; }`

Provide implementations:
- `FiveCardDrawPlayService : IGamePlayService`
- `TwosJacksManWithTheAxePlayService : IGamePlayService`
- `SevenCardStudPlayService : IGamePlayService`

`SevenCardStudPlayService.SupportsDraw = false`.

#### 6.1.2 Factory
Register `IGamePlayServiceFactory`:
- `IGamePlayService Resolve(string gameTypeCode)`

Implementation can be dictionary-based.

### 6.2 Program.cs DI registration
In `CardGames.Poker.Web/Program.cs`:
- Register Refit client for `ISevenCardStudApi` exactly like other clients.
- Register `SevenCardStudPlayService` and add to factory.

### 6.3 TablePlay.razor
Update:
- Inject factory instead of multiple APIs.
- In `LoadDataAsync` after rules are loaded (or after table state), resolve `_gamePlayService`.
- Update `HandlePlayerActionAsync` to call `_gamePlayService.ActionAsync`.
- Ensure draw-panel logic is gated by `SupportsDraw` as well as `PhaseCategory=="Drawing"`.

### 6.4 Card mapping for private state
Update `HandlePrivateStateUpdatedAsync` and `LoadDataAsync`:
- Stop forcing `IsFaceUp = true`.
- Instead map flags from DTO:
  - `IsFaceUp` as received.
  - `IsPubliclyVisible` as received (default fallback as defined above).

### 6.5 TableSeat.razor: rendering hole cards
Update rendering rules:
- If card belongs to the current player (seat owner): show face always.
- Else if `card.IsPubliclyVisible == true`: show face.
- Else show back.

Also update placeholder card count:
- Replace hardcoded `for (i < 5)` with `for (i < MaxCardsPerPlayer)`.

Get `MaxCardsPerPlayer` from game rules:
- Prefer using rules DTO: `rules.MaxCards`.
- Fallback: `7` if missing.

### 6.6 ActionPanel / UI text
- Ensure action panel does not show draw/discard UI for Stud.
- Ensure phase labels show `ThirdStreet`, etc.
- Support bring-in  (ante):
  - If bring-in  (ante) is represented as a forced bet, ensure UI shows min bet set to bring-in  (ante).

### 6.7 TableCanvas
No community cards for stud; ensure community area is hidden or empty.

---

## 7) Data flows and DTO shapes (contract)

### 7.1 Required DTO fields for seats’ cards
For each card delivered to clients:
- `Rank` (or equivalent)
- `Suit` (or equivalent)
- `IsFaceUp`
- `IsPubliclyVisible`

Public state may omit rank/suit for hidden cards; private state for the seat owner must include actual values.

### 7.2 Private vs public channel
- Public table state message should never leak hidden cards.
- Private state message should include full info for that player.

---

## 8) Testing plan

### 8.1 Unit tests (poker engine)
Add tests in `Tests/` project (identify existing poker tests pattern):
- `SevenCardStud_InitialDeal_Has2Down1UpPerPlayer`
- `SevenCardStud_ThirdStreet_BringInIsLowestUpcard`
- `SevenCardStud_StreetProgression_DealsCorrectNumberOfUpAndDownCards`
- `SevenCardStud_Showdown_EvaluatesBestOfSeven`

### 8.2 Contract/UI tests
- A rendering test is optional, but at least add a small component-level test if test infra exists.

### 8.3 Manual smoke
- Create stud table, join 2 players.
- Verify:
  - Each sees their own 2 hole cards.
  - Opponent sees 2 backs.
  - Third card is face-up for both.
  - Streets add correct upcards.
  - River adds downcard visible only to owner until showdown.

---

## 9) Implementation checklist (ordered)

1. Contracts
   - Add `IsPubliclyVisible`.
   - Add `ISevenCardStudApi`.
2. API
   - Add/route stud action endpoint.
   - Add mapping for stud state.
3. Engine
   - Implement dealing, phases, bring-in  (ante), action order.
   - Ensure state builder publishes correct visibility.
4. Web
   - Add strategy (`IGamePlayService`) + factory.
   - Register Refit + services.
   - Update `TablePlay.razor` to use service.
   - Update `TableSeat.razor` rendering + placeholder count.
5. Tests
   - Engine unit tests.
   - Run solution build.

---

## 10) Exact decisions needed before coding (resolve up front)

To avoid ambiguity, confirm these in code as constants/config:
- Suit order for bring-in  (ante) tiebreak.
- bring-in  (ante) amount and whether it’s fixed/derived from blinds.
- Whether showdown reveals all downcards.
- How public state represents hidden cards (omitted vs placeholder objects).

---
