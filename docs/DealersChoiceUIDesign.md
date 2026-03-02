# Dealer's Choice — UI Design & Flow Analysis

**Author:** Linus (Frontend Dev)  
**Requested by:** Rob Gibbens  
**Date:** 2026-03-02  

---

## 1. CreateTable.razor Changes

### Current Flow
The create-table page follows a 3-step wizard:
1. **Choose Game Variant** — grid of `GetAvailablePokerGamesResponse` cards (name, description, image, player range)
2. **Table Settings** — table name, ante, min bet (shown after variant selected)
3. **Add Players** — seat list with names/chips (shown after variant selected)

The variant grid collapses to show only the selected card after selection. Submission calls `CreateGameCommand(ante, gameCode, gameId, gameName, minBet, players)`.

### Proposed: "Dealer's Choice" as a Special Variant Card

Add **one hardcoded variant card** at the **beginning** of the `VariantCards` enumerable, before the API-returned games. This card is NOT fetched from the `AvailableGamesApi` — it's a client-side constant.

```
Name:        "Dealer's Choice"
Code:        "DEALERS_CHOICE"
Description: "The dealer picks the game, ante, and minimum bet before each hand"
ImageName:   "dealerschoice.png"  (new image — a dealer chip or rotating card icon)
MinPlayers:  2
MaxPlayers:  8  (union of all available games)
```

**Why a hardcoded card?** Dealer's Choice isn't a real game type in the domain engine. It's a table-level mode. The backend will store this as a table property (`IsDealersChoice = true`), not as a game type code. The first hand's game type gets set when the first dealer makes their choice. This keeps game rules in the domain and table mode in the API/UI.

### Flow When DC Is Selected

| Step | Standard Game | Dealer's Choice |
|------|--------------|-----------------|
| 1. Variant | Select specific game | Select "Dealer's Choice" card |
| 2. Settings | Table name, ante, min bet | Table name **only** (ante/min bet hidden — dealer sets these per hand) |
| 3. Players | Same | Same |
| Create | `CreateGameCommand(ante, "FIVECARDDRAW", ...)` | `CreateGameCommand(0, "DEALERS_CHOICE", ...)` with ante=0 as placeholder |

**Key changes to `CreateTable.razor`:**

```csharp
// New constant at top of @code block
private static readonly GetAvailablePokerGamesResponse DealersChoiceCard = new(
    code: "DEALERS_CHOICE",
    description: "The dealer picks the game, ante, and minimum bet before each hand",
    imageName: "dealerschoice.png",
    maximumNumberOfPlayers: 8,
    minimumNumberOfPlayers: 2,
    name: "Dealer's Choice"
);

private bool IsDealersChoice => _selectedVariant?.Code == "DEALERS_CHOICE";

// Modify VariantCards property
private IEnumerable<GetAvailablePokerGamesResponse> VariantCards
{
    get
    {
        if (_selectedVariant is not null)
            return [_selectedVariant];
        
        // Dealer's Choice first, then API games
        return new[] { DealersChoiceCard }
            .Concat(_availableGames.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase));
    }
}
```

In the Step 2 markup, conditionally hide ante/min bet when `IsDealersChoice`:

```razor
@if (!IsDealersChoice)
{
    <div class="form-row two-columns">
        <!-- existing ante + min bet inputs -->
    </div>
}
else
{
    <div class="dc-info-banner">
        <i class="fa-regular fa-shuffle"></i>
        <span>The dealer will choose the game, ante, and minimum bet before each hand.</span>
    </div>
}
```

The `CanCreate` validation also needs to skip ante/minBet checks when DC:

```csharp
private bool CanCreate =>
    _selectedVariant is not null &&
    !string.IsNullOrWhiteSpace(_tableName) &&
    _players.All(p => !string.IsNullOrWhiteSpace(p.Name) && p.StartingChips > 0) &&
    (IsDealersChoice || (_ante > 0 && _minBet > 0));
```

### Visual Treatment of the DC Card

Give the Dealer's Choice card a **distinct visual** in the grid — a shimmering border or gradient accent (`variant-card dc-special`) so it stands out from regular game variants. Use a `<span class="variant-badge dc-badge">Rotating</span>` badge instead of "Coming Soon".

---

## 2. Dealer Choice Modal Design

### When It Appears

After showdown completes and before the next hand starts. Today, the between-hands flow is:
1. Showdown results display (8-second countdown via `ContinuousPlayResultsDisplayDurationSeconds`)
2. Complete phase → continuous play service starts next hand automatically

With Dealer's Choice, the flow becomes:
1. Showdown results display (same)
2. **NEW: DealerChoiceRequired phase** — server pauses the continuous play pipeline
3. Dealer sees choice modal; other players see waiting overlay
4. Dealer submits choice → **DealerChoiceMade** event broadcast
5. Brief announcement (2-3 seconds) showing what was chosen
6. CollectingAntes → Dealing → normal hand flow with the chosen game type

### DealerChoiceModal.razor — What the Dealer Sees

A full-screen overlay (consistent with existing overlay patterns like `ShowdownOverlay`, `BuyCardOverlay`) with three sections:

```
┌─────────────────────────────────────────────────┐
│  🎲 Your Turn to Deal — Pick the Game!          │
│                                                   │
│  [Game Variant Grid]                              │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐    │
│  │5-Card  │ │7-Card  │ │Kings & │ │Good Bad│    │
│  │Draw    │ │Stud    │ │Lows    │ │Ugly    │    │
│  └────────┘ └────────┘ └────────┘ └────────┘    │
│  ┌────────┐ ┌────────┐ ┌────────┐                │
│  │Follow  │ │Baseball│ │2s Jacks│                │
│  │Queen   │ │        │ │ManAxe  │                │
│  └────────┘ └────────┘ └────────┘                │
│                                                   │
│  Ante: [___5___]    Min Bet: [___10___]           │
│                                                   │
│  ⏱️ 45 seconds remaining                         │
│                                                   │
│  [        Deal This Hand        ]                 │
└─────────────────────────────────────────────────┘
```

**Component structure:**

```razor
@* DealerChoiceModal.razor *@
<div class="table-overlay dealer-choice-overlay" role="dialog" aria-modal="true">
    <div class="overlay-content dealer-choice-content">
        <header class="dealer-choice-header">
            <div class="dealer-choice-icon"><i class="fa-solid fa-shuffle"></i></div>
            <h2>Your Turn to Deal</h2>
            <p class="dealer-choice-subtitle">Pick the game for this hand</p>
        </header>

        <!-- Game selection grid (reuses variant-card styling from CreateTable) -->
        <section class="dealer-choice-games">
            <div class="variants-grid compact">
                @foreach (var game in AvailableGames)
                {
                    <div class="variant-card @(SelectedGame?.Code == game.Code ? "selected" : "")"
                         @onclick="() => SelectGame(game)">
                        <!-- same card internals as CreateTable variant cards -->
                    </div>
                }
            </div>
        </section>

        <!-- Ante / Min Bet inputs -->
        <section class="dealer-choice-settings">
            <div class="form-row two-columns">
                <div class="form-group">
                    <label>Ante</label>
                    <input type="number" min="1" max="100" @bind="Ante" />
                </div>
                <div class="form-group">
                    <label>Min Bet</label>
                    <input type="number" min="1" max="1000" @bind="MinBet" />
                </div>
            </div>
        </section>

        <!-- Timer -->
        <div class="dealer-choice-timer">
            <i class="fa-regular fa-clock"></i>
            <span>@SecondsRemaining seconds remaining</span>
            <div class="timer-bar" style="width: @TimerPercentage%"></div>
        </div>

        <!-- Submit -->
        <button class="btn btn-primary btn-lg" 
                @onclick="SubmitChoice"
                disabled="@(SelectedGame is null || IsSubmitting)">
            <i class="fa-regular fa-play"></i>
            Deal This Hand
        </button>
    </div>
</div>
```

**Parameters:**

```csharp
@code {
    [Parameter] public IReadOnlyList<GetAvailablePokerGamesResponse> AvailableGames { get; set; }
    [Parameter] public int SecondsRemaining { get; set; }
    [Parameter] public int DefaultAnte { get; set; } = 5;
    [Parameter] public int DefaultMinBet { get; set; } = 10;
    [Parameter] public EventCallback<DealerChoiceSelection> OnChoiceMade { get; set; }
    [Parameter] public bool IsSubmitting { get; set; }
}
```

### Timer Behavior

- **Duration:** 60 seconds (configurable server-side)
- **Timeout action:** Server auto-selects the previous hand's game type with same ante/min bet (or a random game if first hand). This prevents indefinite waiting.
- **Visual:** Countdown number + shrinking progress bar (same pattern as `ActionTimerStateDto`)

### DealerChoiceSelection DTO

```csharp
public record DealerChoiceSelection(
    string GameTypeCode,
    int Ante,
    int MinBet
);
```

---

## 3. DealerChoiceWaiting.razor — What Non-Dealers See

A lighter overlay (similar to the existing "Waiting for Players" overlay pattern):

```
┌─────────────────────────────────────────────────┐
│                                                   │
│         🎲                                        │
│   Waiting for Rob to pick the next game...        │
│                                                   │
│         ⏱️ 42 seconds                             │
│         ▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░                     │
│                                                   │
└─────────────────────────────────────────────────┘
```

```razor
@* DealerChoiceWaiting.razor *@
<div class="table-overlay dealer-choice-waiting-overlay">
    <div class="overlay-content">
        <div class="overlay-icon"><i class="fa-solid fa-shuffle fa-spin-pulse"></i></div>
        <h2>Dealer's Choice</h2>
        <p>Waiting for <strong>@DealerName</strong> to pick the next game...</p>
        @if (SecondsRemaining > 0)
        {
            <div class="dealer-choice-timer">
                <span>@SecondsRemaining seconds</span>
                <div class="timer-bar" style="width: @TimerPercentage%"></div>
            </div>
        }
    </div>
</div>
```

**Parameters:**

```csharp
@code {
    [Parameter] public string DealerName { get; set; }
    [Parameter] public int SecondsRemaining { get; set; }
    [Parameter] public int TotalSeconds { get; set; } = 60;
    private int TimerPercentage => TotalSeconds > 0 ? (SecondsRemaining * 100) / TotalSeconds : 0;
}
```

---

## 4. Table State Display Changes (TablePlay.razor)

### Dynamic Game Type Per Hand

The existing info strip already shows game type:

```razor
<span class="table-game-type"> (@_gameResponse.GameTypeName)</span>
```

For DC tables, this needs to reflect the **current hand's** game type, not the table's creation-time type. The `TableStatePublicDto` already broadcasts `GameTypeName` and `GameTypeCode` — the server just needs to update these per hand when in DC mode.

**Proposed changes to the info strip for DC tables:**

```razor
<span class="table-game-type">
    @if (_isDealersChoice)
    {
        <i class="fa-regular fa-shuffle"></i>
        <span>Dealer's Choice</span>
        @if (!string.IsNullOrWhiteSpace(_currentHandGameTypeName))
        {
            <span class="dc-current-game"> — @_currentHandGameTypeName</span>
        }
    }
    else
    {
        <span>(@_gameResponse.GameTypeName)</span>
    }
</span>
```

### Ante/Min Bet Display

For DC tables, the blinds pill in the info strip should update per hand (from the SignalR table state), since the dealer sets these each hand:

```razor
<span class="table-blinds-pill">
    <span class="table-blinds-line">Ante: @(_tableState?.Ante ?? _ante)</span>
    <span class="table-blinds-line">Min Bet: @(_tableState?.MinBet ?? _gameResponse?.MinBet)</span>
</span>
```

### Game Type Announcement Between Hands

After the dealer makes their choice and before dealing begins, show a brief announcement toast or banner:

```
🎲 Rob chose Five Card Draw — Ante: 10, Min Bet: 25
```

This uses the existing `ShowToastAsync()` method, triggered by the `DealerChoiceMade` SignalR event.

### Who Is the DC Dealer

The existing dealer button indicator on `TableSeat` suffices — it already shows a "D" chip on the dealer's seat. No additional visual needed since the dealer rotates normally.

---

## 5. SignalR Integration

### New Events

Two new events on `GameHubClient`:

| Event | DTO | Who Receives | When |
|-------|-----|-------------|------|
| `DealerChoiceRequired` | `DealerChoiceRequiredDto` | All players in game group | After showdown, before next hand, DC table only |
| `DealerChoiceMade` | `DealerChoiceMadeDto` | All players in game group | After dealer submits their choice |

### DTOs (add to `CardGames.Contracts/SignalR/`)

```csharp
// DealerChoiceRequiredDto.cs
public sealed record DealerChoiceRequiredDto
{
    public required Guid GameId { get; init; }
    public required int DealerSeatIndex { get; init; }
    public required string DealerName { get; init; }
    public required int TimeoutSeconds { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required IReadOnlyList<AvailableGameDto> AvailableGames { get; init; }
    
    // Previous hand's settings as defaults
    public int? PreviousAnte { get; init; }
    public int? PreviousMinBet { get; init; }
    public string? PreviousGameTypeCode { get; init; }
}

public sealed record AvailableGameDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? ImageName { get; init; }
}

// DealerChoiceMadeDto.cs
public sealed record DealerChoiceMadeDto
{
    public required Guid GameId { get; init; }
    public required string DealerName { get; init; }
    public required string GameTypeCode { get; init; }
    public required string GameTypeName { get; init; }
    public required int Ante { get; init; }
    public required int MinBet { get; init; }
}
```

### GameHubClient Changes

```csharp
// New events
public event Func<DealerChoiceRequiredDto, Task>? OnDealerChoiceRequired;
public event Func<DealerChoiceMadeDto, Task>? OnDealerChoiceMade;

// In ConnectAsync(), add subscriptions:
_hubConnection.On<DealerChoiceRequiredDto>("DealerChoiceRequired", HandleDealerChoiceRequired);
_hubConnection.On<DealerChoiceMadeDto>("DealerChoiceMade", HandleDealerChoiceMade);

// New send method for dealer's choice submission
public async Task SubmitDealerChoiceAsync(Guid gameId, string gameTypeCode, int ante, int minBet)
{
    await _hubConnection!.InvokeAsync("SubmitDealerChoice", gameId, gameTypeCode, ante, minBet);
}
```

### TablePlay.razor Event Handling

```csharp
// New state fields
private bool _isDealersChoice;
private bool _isDealerChoiceRequired;
private DealerChoiceRequiredDto? _dealerChoiceRequest;
private string? _currentHandGameTypeName;

// In OnInitializedAsync, subscribe:
GameHubClient.OnDealerChoiceRequired += HandleDealerChoiceRequiredAsync;
GameHubClient.OnDealerChoiceMade += HandleDealerChoiceMadeAsync;

private async Task HandleDealerChoiceRequiredAsync(DealerChoiceRequiredDto evt)
{
    if (evt.GameId != GameId) return;
    
    _isDealerChoiceRequired = true;
    _dealerChoiceRequest = evt;
    await InvokeAsync(StateHasChanged);
}

private async Task HandleDealerChoiceMadeAsync(DealerChoiceMadeDto evt)
{
    if (evt.GameId != GameId) return;
    
    _isDealerChoiceRequired = false;
    _dealerChoiceRequest = null;
    _currentHandGameTypeName = evt.GameTypeName;
    _gameTypeCode = evt.GameTypeCode;  // Update for game-type-specific UI logic
    _ante = evt.Ante;
    
    // Announce to all players
    await ShowToastAsync(
        $"🎲 {evt.DealerName} chose {evt.GameTypeName} — Ante: {evt.Ante}, Min Bet: {evt.MinBet}", 
        "info", 
        3000
    );
    
    await InvokeAsync(StateHasChanged);
}
```

### Overlay Rendering in TablePlay.razor

Add after the showdown overlay block and before the paused overlay:

```razor
<!-- Dealer's Choice overlays -->
@if (_isDealerChoiceRequired && _dealerChoiceRequest is not null)
{
    @if (IsCurrentPlayerDealer)
    {
        <DealerChoiceModal 
            AvailableGames="@_dealerChoiceRequest.AvailableGames"
            SecondsRemaining="@GetDealerChoiceSecondsRemaining()"
            DefaultAnte="@(_dealerChoiceRequest.PreviousAnte ?? 5)"
            DefaultMinBet="@(_dealerChoiceRequest.PreviousMinBet ?? 10)"
            OnChoiceMade="HandleDealerChoiceSubmitAsync"
            IsSubmitting="@_isSubmittingDealerChoice" />
    }
    else
    {
        <DealerChoiceWaiting 
            DealerName="@_dealerChoiceRequest.DealerName"
            SecondsRemaining="@GetDealerChoiceSecondsRemaining()"
            TotalSeconds="@_dealerChoiceRequest.TimeoutSeconds" />
    }
}
```

Where `IsCurrentPlayerDealer` is:

```csharp
private bool IsCurrentPlayerDealer => _currentPlayerSeatIndex == _dealerSeatIndex;
```

### Game Type Code Transition

The critical lifecycle for `_gameTypeCode` in DC mode:

1. **Table creation:** `_gameTypeCode = "DEALERS_CHOICE"` — no game-specific flags are true
2. **DealerChoiceMade received:** `_gameTypeCode` updates to the chosen code (e.g., `"FIVECARDDRAW"`)
3. **All existing game-specific logic** (`IsFiveCardDraw`, `IsKingsAndLows`, etc.) immediately becomes active for the current hand
4. **Hand completes, showdown ends:** `_gameTypeCode` stays as the last played game until the next `DealerChoiceMade`
5. **DealerChoiceRequired received:** game-specific overlays are suppressed (the DC overlay takes priority)

This means the existing overlay/panel system works unchanged — it already switches on `_gameTypeCode`. The only new condition is showing the DC overlay between hands.

---

## 6. Component Architecture Summary

### New Blazor Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `DealerChoiceModal.razor` | `Components/Shared/` | Full-screen overlay for dealer to pick game, ante, min bet |
| `DealerChoiceWaiting.razor` | `Components/Shared/` | Waiting overlay for non-dealers |

### New DTOs

| DTO | Location | Purpose |
|-----|----------|---------|
| `DealerChoiceRequiredDto` | `Contracts/SignalR/` | Broadcast when DC dealer must choose |
| `DealerChoiceMadeDto` | `Contracts/SignalR/` | Broadcast when choice is submitted |
| `DealerChoiceSelection` | `Contracts/` | Client → server choice payload |

### Modified Files

| File | Change |
|------|--------|
| `CreateTable.razor` | Add DC card to variant grid, hide ante/min bet when DC selected, adjust validation |
| `TablePlay.razor` | Add DC state fields, event handlers, conditional overlay rendering, info strip updates |
| `GameHubClient.cs` | Add `OnDealerChoiceRequired`/`OnDealerChoiceMade` events, `SubmitDealerChoiceAsync` method |
| `TableStatePublicDto.cs` | Optionally add `IsDealersChoice` bool (or derive from game type code) |
| `app.css` | Styles for `.dealer-choice-overlay`, `.dealer-choice-waiting-overlay`, `.dc-special` variant card |

### New Static Asset

| Asset | Location |
|-------|----------|
| `dealerschoice.png` | `wwwroot/images/games/` |

### Integration with Existing Patterns

- **Overlays:** Both new components follow the existing `table-overlay` → `overlay-content` pattern used by `ShowdownOverlay`, `BuyCardOverlay`, `DropOrStayOverlay`, etc.
- **Timer:** Reuses the `ActionTimerStateDto` pattern and visual style
- **Game grid:** The modal's game grid reuses `variant-card` CSS from CreateTable
- **SignalR:** Follows the existing `GameHubClient` event delegation pattern (event → handler → InvokeAsync(StateHasChanged))
- **Toast announcements:** Uses the existing `ShowToastAsync` method
- **Continuous Play:** Integrates with the existing `ContinuousPlayBackgroundService` — server pauses the pipeline during the DC choice window and resumes after choice or timeout

---

## 7. State Machine — Between-Hands DC Flow

```
  [Showdown Complete]
        │
        ▼
  [Results Phase / Countdown]   ← same as today
        │
        ▼
  ─── Is Dealer's Choice table? ───
  │ No                        │ Yes
  │                           │
  ▼                           ▼
  [CollectingAntes]    [DealerChoiceRequired]
  (normal flow)         ├── Dealer sees: DealerChoiceModal
                        └── Others see: DealerChoiceWaiting
                              │
                        (choice made OR timeout)
                              │
                              ▼
                        [DealerChoiceMade broadcast]
                        - _gameTypeCode updated
                        - ante/minBet updated  
                        - toast announcement
                              │
                              ▼
                        [CollectingAntes]  ← with new game type
                        (normal flow continues)
```

---

## 8. Edge Cases

| Scenario | Handling |
|----------|----------|
| Dealer disconnects during choice | Server timeout (60s) auto-selects previous game |
| Dealer's first hand (no previous game) | Server picks random from available, or forces dealer to choose |
| Player count changes between hands | Modal's available games should respect current player count vs game's min/max players |
| Dealer sits out | Skip to next non-sitting-out dealer for the choice |
| Only one game type available | Auto-select it, still let dealer set ante/min bet |
| Game type changes mid-session | All game-specific UI (draw panel, overlays, wild cards) adapts via existing `_gameTypeCode` switches |
