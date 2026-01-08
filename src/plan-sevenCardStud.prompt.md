# Plan: Refactor Web Project for Seven Card Stud Support


This plan refactors TablePlay.razor and related components to remove hardcoded dependencies on Five Card Draw, adds support for "Hole Cards" (visible to player but face-down to others), and abstracts API interactions.

## Steps

- Create a IGamePlayService interface and implementations (e.g., FiveCardDrawPlayService) to abstract game-specific API calls (Action, Draw) currently hardcoded in TablePlay.razor.
- Register a factory or strategy in Program.cs to resolve IGamePlayService based on GameTypeCode (e.g., "SEVENCARDSTUD").
- Update PrivateStateDto and CardInfo to include IsPubliclyVisible (in addition to IsFaceUp), allowing players to distinguish "Hole Cards" from "Face Up Cards".
- Update TablePlay.razor methods HandlePrivateStateUpdatedAsync and LoadDataAsync to map this new visibility flag instead of forcing IsFaceUp = true.
- Update TableSeat.razor to use IsPubliclyVisible for rendering: "Hole Cards" are shown to the owner (maybe dimmed/marked) but hidden to others, while public cards are shown to all.
- Verify ActionPanel.razor and TableCanvas.razor correctly handle variable numbers of cards (Stud has 7) and non-Draw phases (Studio has no "Draw" phase).

## Further Considerations
- Dealing Logic: Seven Card Stud has multiple dealing rounds. Ensure TableStateBuilder (backend) correctly sets CurrentPhase (e.g. "ThirdStreet") so the UI updates the message.
- Bring-In Bets: Stud has a forced "Bring-In" bet. Ensure ActionPanel can display this special minimum bet action if not covered by standard "Bet" types.
- Hardcoded Assumptions Identified:
  - TablePlay.razor: Injects specific API clients (IFiveCardDrawApi).
  - TablePlay.razor: HandlePlayerActionAsync switches on IsTwosJacks.../else FiveCard.
  - TableSeat.razor: Assumes IsFaceUp false means "Show Back", preventing players from seeing their own hole cards if marked correctly by the server.


## 1. Web Project Configuration (`Program.cs`)
- **API Client Registration**: The application uses `Refit` to generate API clients. Each game has a specific interface registered in `Program.cs`.
    - **Change Needed**: Register the new `ISevenCardStudApi` client.
    - **Current Pattern**:
      ```csharp
      builder.Services.AddRefitClient<IFiveCardDrawApi>(...)
      ```

## 2. Main Game Component (`TablePlay.razor`)
This file contains the core game loop logic and has several touchpoints for game-specific behavior.

### API Dependencies
- **Client Injection**: Specific clients are injected (`IFiveCardDrawApi`, `ITwosJacksManWithTheAxeApi`, etc.).
    - **Change Needed**: Inject `ISevenCardStudApi`.
- **Action Handling**: `HandlePlayerActionAsync` switches on the game type string to determine which client to call.
    - **Change Needed**: Add a case for Seven Card Stud to `HandlePlayerActionAsync`.
    - **Refactoring Opportunity**: Consider a generic `IGameActionApi` wrapper to avoid growing this switch statement indefinitely.

### Game Phase Logic
- **Drawing Phase**: The code assumes extensive logic for a "Drawing" phase (selecting discards, draw panel).
    - **Assumption**: `IsDrawingPhase` logic is currently checking `PhaseCategory == "Drawing"`. Seven Card Stud does not have a draw phase (discard/replace), but rather multiple "Dealing" phases.
    - **Impact**: Ensure the server sends a phase category other than "Drawing" for the dealing rounds, or `TablePlay` might show the Draw UI incorrectly if phases are mislabeled.
    - **Draw Panel**: Logic for `_forceShowDrawPanel` and `isDrawAnimating` might need protection so it doesn't trigger for Stud games.

### Card Limits
- **Max Discards**: `GetMaxDiscards()` has a fallback of `private int maxDiscards = 3;`. While not directly used in Stud, this highlights "5-card draw" default thinking.

## 3. Seat & Card Visualization (`TableSeat.razor`)
- **Placeholder Cards**: The component hardcodes the number of card placeholders (ghost cards) to 5.
    - **Code**:
      ```razor
      @for (int i = 0; i < 5; i++) { ... }
      ```
    - **Change Needed**: This loop must be dynamic based on the game type or maximum possible cards (7 for Stud).
- **Card Fanning**: The CSS class `.cards-fan` and the logic inside `TableSeat` simply iterates over all cards.
    - **Assumption**: 5 cards fit nicely in the container.
    - **Impact**: 7 cards might look crowded or overflow. Visual testing will be required.

## 4. Game Rules & Metadata
- **`GetGameRulesResponse`**: The `GameRulesService` fetches rules. Ensure the backend returns correct `MaxPlayers` and `CardDealing` config for Seven Card Stud.
- **Table Canvas**: The `TableCanvas` component has a comment `<!-- Community cards area (for future variants like Hold'em) -->`. Seven Card Stud does NOT need this, but it's good to note it is reserved.

## 5. API Contracts (Reference)
- You will likely need to generate a new `Refit` interface (e.g., `ISevenCardStudApi`) matching the pattern of `IFiveCardDrawApi`.

## Summary of Work
1.  **Backend**: Create `ISevenCardStudApi` and implementation (outside scope of this analysis, but required).
2.  **Web `Program.cs`**: Register `ISevenCardStudApi`.
3.  **Web `TablePlay.razor`**:
    -   Inject `ISevenCardStudApi`.
    -   Update `HandlePlayerActionAsync` to support the new game.
4.  **Web `TableSeat.razor`**:
    -   Change the hardcoded `5` loop for placeholders to be dynamic or a higher constant (e.g., `7` or `MaxCards`).

