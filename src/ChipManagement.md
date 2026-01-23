## Plan: Chip Management Dashboard Feature

Add a new collapsible "Chips" section to the Dashboard panel that displays the player's current chip stack, allows adding chips (with game-state restrictions), and shows a chip history graph tracking chip changes hand-by-hand for the current game.

### Steps

1. **Create `AddChips` API Command** in [CardGames.Poker.Api/Features/Games/Common/v1/Commands/AddChips/](CardGames.Poker.Api/Features/Games/Common/v1/Commands/)
   - Add `AddChipsCommand` with `GameId`, `PlayerId`, `Amount` properties
   - Add `AddChipsCommandHandler` that validates game state and applies chips
   - Only allow immediate chip addition for Kings and Lows (`GameTypeCode == "KINGSANDLOWS"`)
   - For other games (Five Card Draw, Seven Card Stud, Twos Jacks Man with the Axe): queue chips and apply only when `GameStatus == BetweenHands`
   - Extend `GamePlayer` entity with `PendingChipsToAdd` property for deferred chip additions
   - Broadcast updated state via `IGameStateBroadcaster.BroadcastGameStateAsync()`

2. **Create `ChipHistoryDto` and extend DTOs** in [CardGames.Contracts/SignalR/](CardGames.Contracts/SignalR/)
   - Add `ChipHistoryEntryDto` with `HandNumber`, `ChipStackAfterHand`, `ChipsDelta`, `Timestamp` properties
   - Add `ChipHistoryDto` containing `CurrentChips`, `PendingChipsToAdd`, `History` list
   - Extend `PrivateStateDto` to include `ChipHistory` for the current game session
   - Populate chip history from `HandHistoryEntryDto.NetAmount` data per hand

3. **Add SignalR event for chip changes** in [CardGames.Poker.Api/Services/GameStateBroadcaster.cs](CardGames.Poker.Api/Services/GameStateBroadcaster.cs)
   - Add `ChipsAdded` event to notify when chips are added (for toast notifications)
   - Update `GameHubClient.cs` on web side to handle new event
   - Include pending chip notification in `PrivateStateUpdated` broadcasts

4. **Create `ChipManagementSection.razor`** in [CardGames.Poker.Web/Components/Shared/](CardGames.Poker.Web/Components/Shared/)
   - Display current chip stack (from `TableStatePublicDto.Seats[playerSeatIndex].Chips`)
   - Add input field for chip amount with validation (positive integers only)
   - Add "Add Chips" button that calls API endpoint
   - Show pending chips indicator when chips are queued for between-hands application
   - Display info message explaining when chips will be applied based on game type

5. **Create chip history graph component** in `ChipManagementSection.razor`
   - Use CSS-based bar/line chart following existing `OddsSection.razor` bar pattern
   - X-axis: Hand numbers (1, 2, 3, ...)
   - Y-axis: Chip stack value at end of each hand
   - Highlight positive (green) vs negative (red) chip deltas
   - Show starting stack as hand 0 baseline
   - Consider using a simple SVG polyline for the trend line

6. **Integrate into Dashboard** in [CardGames.Poker.Web/Components/Pages/TablePlay.razor](CardGames.Poker.Web/Components/Pages/TablePlay.razor#L144-L165)
   - Add `ChipManagementSection` as first section inside `DashboardPanel`, above Leaderboard
   - Register "Chips" in `DashboardState._sectionStates` dictionary with default expanded=true
   - Pass `GameTypeCode` to section for determining chip addition timing rules
   - Wire up chip history data from SignalR state updates

### Further Considerations

1. **Pending chips UX**: Should pending chips show as a separate "queued" value or be visually merged with current stack with a pending indicator? Recommend: show separately with "(+X pending)" label.

2. **Chart library vs CSS**: Should we add a chart library (e.g., Chart.js via JS interop) for smoother graphs, or keep it pure CSS/SVG? Recommend: start with CSS/SVG bars matching existing pattern, upgrade later if needed.

3. **History depth**: How many hands of chip history to display? Recommend: last 20 hands with scroll, or the entire current game session if fewer.
