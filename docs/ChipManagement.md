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
   - Add `ChipHistoryDto` containing `CurrentChips`, `PendingChipsToAdd`, `History` list (last 30 hands)
   - Extend `PrivateStateDto` to include `ChipHistory` for the current game session
   - Populate chip history from `HandHistoryEntryDto.NetAmount` data per hand

3. **Add SignalR event for chip changes** in [CardGames.Poker.Api/Services/GameStateBroadcaster.cs](CardGames.Poker.Api/Services/GameStateBroadcaster.cs)
   - Add `ChipsAdded` event to notify when chips are added (for toast notifications)
   - Update `GameHubClient.cs` on web side to handle new event
   - Include pending chip notification in `PrivateStateUpdated` broadcasts

4. **Add Blazor-ApexCharts NuGet package** to [CardGames.Poker.Web/CardGames.Poker.Web.csproj](CardGames.Poker.Web/CardGames.Poker.Web.csproj)
   - Install `Blazor-ApexCharts` package from https://github.com/apexcharts/Blazor-ApexCharts
   - Register ApexCharts services in `Program.cs`
   - Add required CSS/JS references in `App.razor` or `_Host.cshtml`

5. **Create `ChipManagementSection.razor`** in [CardGames.Poker.Web/Components/Shared/](CardGames.Poker.Web/Components/Shared/)
   - Display current chip stack with pending chips inline: `5,035 (+500 pending)`
   - Add input field for chip amount with validation (positive integers only)
   - Add "Add Chips" button that calls API endpoint
   - Display info message explaining when chips will be applied based on game type
   - Create chip history line chart using ApexCharts:
     - X-axis: Hand numbers (1, 2, 3, ... up to 30)
     - Y-axis: Chip stack value at end of each hand
     - Show starting stack as hand 0 baseline
     - Use green/red color coding for positive/negative trends
     - Enable scrolling for last 30 hands of history

6. **Integrate into Dashboard** in [CardGames.Poker.Web/Components/Pages/TablePlay.razor](CardGames.Poker.Web/Components/Pages/TablePlay.razor#L144-L165)
   - Add `ChipManagementSection` as first section inside `DashboardPanel`, above Leaderboard
   - Register "Chips" in `DashboardState._sectionStates` dictionary with default expanded=true
   - Pass `GameTypeCode` to section for determining chip addition timing rules
   - Wire up chip history data from SignalR state updates

### Decisions Made

| Decision | Choice |
|----------|--------|
| Pending chips display | Show `(+X pending)` inline next to current stack |
| Chart library | Blazor-ApexCharts |
| History depth | Last 30 hands with scrolling |
