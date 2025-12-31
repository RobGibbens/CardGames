# Dynamic UI Plan: Game-Agnostic Gameplay

This document outlines the design plan for updating the UI to consume game rules dynamically, extending SignalR DTOs with real-time phase/action metadata, and refactoring TablePlay.razor to be data-driven.

## Table of Contents

1. [Overview](#overview)
2. [Part 1: Update UI to Consume Game Rules Dynamically](#part-1-update-ui-to-consume-game-rules-dynamically)
3. [Part 2: Extend SignalR DTOs with Real-Time Phase/Action Metadata](#part-2-extend-signalr-dtos-with-real-time-phaseaction-metadata)
4. [Part 3: Refactor TablePlay.razor to be Data-Driven](#part-3-refactor-tableplayrazor-to-be-data-driven)
5. [Part 4: Overlay System Design](#part-4-overlay-system-design)
6. [Part 5: Handling Various Game Mechanics](#part-5-handling-various-game-mechanics)
7. [Implementation Roadmap](#implementation-roadmap)

---

## Overview

### Current State

The current UI (TablePlay.razor) has hardcoded assumptions:
- Assumes all games follow a Five Card Draw pattern
- Uses game-specific conditionals like `IsTwosJacksManWithTheAxe`
- Has hardcoded phase checks for `GamePhase.WaitingForPlayers`, `GamePhase.Ended`, etc.
- Shows ActionPanel and DrawPanel based on specific phase string comparisons

### Target State

The UI should:
- Fetch game rules on page load and adapt dynamically
- Receive phase/action metadata through SignalR in real-time
- Render overlays and action panels based on phase category, not phase ID
- Support new game types without code changes

---

## Part 1: Update UI to Consume Game Rules Dynamically

### 1.1 Create a Game Rules Service

Create a client-side service that fetches and caches game rules:

**File: `CardGames.Poker.Web/Services/GameRulesService.cs`**

```csharp
public class GameRulesService
{
    private readonly IGamesApi _gamesApi;
    private readonly IMemoryCache _cache;
    
    public async Task<GetGameRulesResponse?> GetGameRulesAsync(string gameTypeCode)
    {
        if (_cache.TryGetValue(gameTypeCode, out GetGameRulesResponse? cached))
            return cached;
            
        var response = await _gamesApi.GetGameRulesAsync(gameTypeCode);
        if (response.IsSuccessStatusCode && response.Content is not null)
        {
            _cache.Set(gameTypeCode, response.Content, TimeSpan.FromHours(1));
            return response.Content;
        }
        return null;
    }
}
```

### 1.2 Inject Game Rules into TablePlay.razor

Modify TablePlay.razor to load game rules on initialization:

```csharp
@inject GameRulesService GameRulesService

@code {
    private GetGameRulesResponse? _gameRules;
    
    private async Task LoadDataAsync()
    {
        // ... existing code ...
        
        // Fetch game rules after we know the game type
        if (!string.IsNullOrWhiteSpace(_gameTypeCode))
        {
            _gameRules = await GameRulesService.GetGameRulesAsync(_gameTypeCode);
        }
    }
}
```

### 1.3 Create Helper Methods for Phase-Based Decisions

Add helper methods that query game rules instead of using hardcoded logic:

```csharp
private bool IsPhaseCategory(string category)
{
    if (_gameRules?.Phases is null || string.IsNullOrEmpty(_gameResponse?.CurrentPhase))
        return false;
        
    var currentPhase = _gameRules.Phases
        .FirstOrDefault(p => p.PhaseId.Equals(_gameResponse.CurrentPhase, StringComparison.OrdinalIgnoreCase));
    
    return currentPhase?.Category.Equals(category, StringComparison.OrdinalIgnoreCase) == true;
}

private bool IsBettingPhase => IsPhaseCategory("Betting");
private bool IsDrawingPhase => IsPhaseCategory("Drawing");
private bool IsDecisionPhase => IsPhaseCategory("Decision");
private bool IsResolutionPhase => IsPhaseCategory("Resolution");
private bool IsSetupPhase => IsPhaseCategory("Setup");

private IReadOnlyList<string> GetCurrentPhaseActions()
{
    if (_gameRules?.Phases is null || string.IsNullOrEmpty(_gameResponse?.CurrentPhase))
        return [];
        
    var currentPhase = _gameRules.Phases
        .FirstOrDefault(p => p.PhaseId.Equals(_gameResponse.CurrentPhase, StringComparison.OrdinalIgnoreCase));
    
    return currentPhase?.AvailableActions ?? [];
}
```

### 1.4 Add Game Rules DTO to Contracts

Ensure the GetGameRulesResponse can be consumed by the Web project:

**File: `CardGames.Contracts/GameRules/GameRulesDto.cs`** (for Refitter-generated client)

The current architecture already has this in the API layer. We need to ensure Refitter generates the client method for GetGameRules.

---

## Part 2: Extend SignalR DTOs with Real-Time Phase/Action Metadata

### 2.1 Extend TableStatePublicDto

Add phase metadata to the public state DTO so clients always have current phase information:

**File: `CardGames.Contracts/SignalR/TableStatePublicDto.cs`**

```csharp
public sealed record TableStatePublicDto
{
    // ... existing properties ...
    
    /// <summary>
    /// The category of the current phase (e.g., "Setup", "Betting", "Drawing", "Decision", "Resolution").
    /// </summary>
    public string? CurrentPhaseCategory { get; init; }
    
    /// <summary>
    /// Whether the current phase requires player action.
    /// </summary>
    public bool CurrentPhaseRequiresAction { get; init; }
    
    /// <summary>
    /// Actions available in the current phase (e.g., ["Check", "Bet", "Call", "Raise", "Fold"]).
    /// </summary>
    public IReadOnlyList<string>? CurrentPhaseAvailableActions { get; init; }
    
    /// <summary>
    /// Configuration for drawing in the current game (if applicable).
    /// </summary>
    public DrawingConfigDto? DrawingConfig { get; init; }
    
    /// <summary>
    /// Whether the game has special rules (like Drop/Stay, Pot Matching, etc.).
    /// </summary>
    public GameSpecialRulesDto? SpecialRules { get; init; }
}

/// <summary>
/// Drawing configuration for the current game.
/// </summary>
public sealed record DrawingConfigDto
{
    public bool AllowsDrawing { get; init; }
    public int? MaxDiscards { get; init; }
    public string? SpecialRules { get; init; }
    public int DrawingRounds { get; init; } = 1;
}

/// <summary>
/// Special rules for the current game.
/// </summary>
public sealed record GameSpecialRulesDto
{
    public bool HasDropOrStay { get; init; }
    public bool HasPotMatching { get; init; }
    public bool HasWildCards { get; init; }
    public string? WildCardsDescription { get; init; }
    public bool HasSevensSplit { get; init; }
}
```

### 2.2 Extend PrivateStateDto

Add action-specific metadata to private state:

**File: `CardGames.Contracts/SignalR/PrivateStateDto.cs`**

```csharp
public sealed record PrivateStateDto
{
    // ... existing properties ...
    
    /// <summary>
    /// Drop or Stay phase information when in decision phase (for Kings and Lows, etc.).
    /// </summary>
    public DropOrStayPrivateDto? DropOrStay { get; init; }
}

/// <summary>
/// Drop or Stay phase information for the player.
/// </summary>
public sealed record DropOrStayPrivateDto
{
    /// <summary>
    /// Whether it is the player's turn to decide.
    /// </summary>
    public bool IsMyTurnToDecide { get; init; }
    
    /// <summary>
    /// Whether the player has already made their decision this round.
    /// </summary>
    public bool HasDecidedThisRound { get; init; }
}
```

### 2.3 Update TableStateBuilder

Modify the TableStateBuilder to include phase metadata from game rules:

**File: `CardGames.Poker.Api/Services/TableStateBuilder.cs`**

```csharp
public async Task<TableStatePublicDto> BuildPublicStateAsync(Game game, ...)
{
    // Get game rules for phase metadata
    GameRules? rules = null;
    if (PokerGameRulesRegistry.TryGet(game.GameTypeCode, out var r))
        rules = r;
    
    var currentPhaseDescriptor = rules?.Phases
        .FirstOrDefault(p => p.PhaseId.Equals(game.CurrentPhase, StringComparison.OrdinalIgnoreCase));
    
    return new TableStatePublicDto
    {
        // ... existing properties ...
        CurrentPhaseCategory = currentPhaseDescriptor?.Category,
        CurrentPhaseRequiresAction = currentPhaseDescriptor?.RequiresPlayerAction ?? false,
        CurrentPhaseAvailableActions = currentPhaseDescriptor?.AvailableActions,
        DrawingConfig = rules?.Drawing != null ? new DrawingConfigDto
        {
            AllowsDrawing = rules.Drawing.AllowsDrawing,
            MaxDiscards = rules.Drawing.MaxDiscards,
            SpecialRules = rules.Drawing.SpecialRules,
            DrawingRounds = rules.Drawing.DrawingRounds
        } : null,
        SpecialRules = BuildSpecialRules(rules)
    };
}

private static GameSpecialRulesDto? BuildSpecialRules(GameRules? rules)
{
    if (rules?.SpecialRules is null || rules.SpecialRules.Count == 0)
        return null;
    
    return new GameSpecialRulesDto
    {
        HasDropOrStay = rules.SpecialRules.ContainsKey("DropOrStay"),
        HasPotMatching = rules.SpecialRules.ContainsKey("LosersMatchPot"),
        HasWildCards = rules.SpecialRules.ContainsKey("WildCards"),
        WildCardsDescription = rules.SpecialRules.TryGetValue("WildCards", out var wc) 
            ? wc?.ToString() : null,
        HasSevensSplit = rules.SpecialRules.ContainsKey("SevensSplit")
    };
}
```

---

## Part 3: Refactor TablePlay.razor to be Data-Driven

### 3.1 Replace Hardcoded Phase Checks

**Before (Hardcoded):**
```razor
@if (isPlayerTurn && !isDrawPhase && _gameResponse.CurrentPhase != GamePhase.WaitingForPlayers.ToString() &&
    _gameResponse.CurrentPhase != GamePhase.Ended.ToString())
{
    <ActionPanel ... />
}
```

**After (Data-Driven):**
```razor
@if (isPlayerTurn && IsBettingPhase)
{
    <ActionPanel ... />
}
```

### 3.2 Replace Game-Specific Conditionals

**Before:**
```csharp
private bool IsTwosJacksManWithTheAxe => string.Equals(_gameTypeCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase);
```

**After:**
```csharp
private bool HasWildCards => _tableState?.SpecialRules?.HasWildCards == true;
private bool HasSevensSplit => _tableState?.SpecialRules?.HasSevensSplit == true;
private bool HasDropOrStay => _tableState?.SpecialRules?.HasDropOrStay == true;
```

### 3.3 Create Phase-Agnostic Action Rendering

Create a universal action panel that renders based on available actions:

**File: `CardGames.Poker.Web/Components/Shared/UniversalActionPanel.razor`**

```razor
@namespace CardGames.Poker.Web.Components.Shared

<div class="action-panel @(IsSubmitting ? "submitting" : "")">
    @foreach (var action in AvailableActions)
    {
        @switch (action.ToUpperInvariant())
        {
            case "CHECK":
                <button class="action-btn check-btn" @onclick="() => SubmitAction(action)" disabled="@IsSubmitting">
                    <i class="action-icon fa-solid fa-check"></i>
                    <span class="action-label">Check</span>
                </button>
                break;
            case "BET":
                <button class="action-btn bet-btn" @onclick="() => SubmitAction(action, betAmount)" disabled="@IsSubmitting">
                    <i class="action-icon fa-solid fa-coins"></i>
                    <span class="action-label">Bet @betAmount</span>
                </button>
                break;
            case "DROP":
                <button class="action-btn fold-btn" @onclick="() => SubmitAction(action)" disabled="@IsSubmitting">
                    <i class="action-icon fa-solid fa-door-open"></i>
                    <span class="action-label">Drop</span>
                </button>
                break;
            case "STAY":
                <button class="action-btn call-btn" @onclick="() => SubmitAction(action)" disabled="@IsSubmitting">
                    <i class="action-icon fa-solid fa-hand"></i>
                    <span class="action-label">Stay</span>
                </button>
                break;
            // ... other actions
        }
    }
</div>

@code {
    [Parameter] public IReadOnlyList<string> AvailableActions { get; set; } = [];
    [Parameter] public EventCallback<(string Action, int? Amount)> OnAction { get; set; }
    [Parameter] public bool IsSubmitting { get; set; }
    // ... additional parameters for bet sizing
}
```

### 3.4 Update TablePlay.razor Main Logic

```csharp
@code {
    // Replace hardcoded phase detection with rules-based detection
    private bool IsBettingPhase => 
        _tableState?.CurrentPhaseCategory?.Equals("Betting", StringComparison.OrdinalIgnoreCase) == true;
    
    private bool IsDrawingPhase => 
        _tableState?.CurrentPhaseCategory?.Equals("Drawing", StringComparison.OrdinalIgnoreCase) == true;
    
    private bool IsDecisionPhase => 
        _tableState?.CurrentPhaseCategory?.Equals("Decision", StringComparison.OrdinalIgnoreCase) == true;
    
    private bool IsSetupPhase =>
        _tableState?.CurrentPhaseCategory?.Equals("Setup", StringComparison.OrdinalIgnoreCase) == true;
    
    private bool IsResolutionPhase =>
        _tableState?.CurrentPhaseCategory?.Equals("Resolution", StringComparison.OrdinalIgnoreCase) == true;
    
    private bool IsSpecialPhase =>
        _tableState?.CurrentPhaseCategory?.Equals("Special", StringComparison.OrdinalIgnoreCase) == true;
}
```

---

## Part 4: Overlay System Design

### 4.1 Overlay Categories

Design overlays based on phase categories, not specific phases:

| Category | Overlay Type | Description |
|----------|--------------|-------------|
| Setup | WaitingOverlay | Players joining, readying up |
| Betting | ActionPanel | Betting actions (check, bet, call, raise, fold) |
| Drawing | DrawPanel | Card selection for discard/draw |
| Decision | DecisionPanel | Game-specific decisions (Drop/Stay) |
| Resolution | ShowdownOverlay | Hand results, winners |
| Special | SpecialPhaseOverlay | Game-specific phases (PlayerVsDeck) |

### 4.2 Create a Generic Decision Panel

**File: `CardGames.Poker.Web/Components/Shared/DecisionPanel.razor`**

```razor
@namespace CardGames.Poker.Web.Components.Shared

<div class="decision-panel @(IsSubmitting ? "submitting" : "")">
    <div class="decision-panel-header">
        <i class="fa-regular fa-circle-question"></i>
        <span>@Title</span>
    </div>
    <div class="decision-panel-subtitle">@Subtitle</div>
    
    <div class="decision-buttons">
        @foreach (var action in AvailableActions)
        {
            <button class="decision-btn @GetButtonClass(action)" 
                    @onclick="() => OnDecision.InvokeAsync(action)" 
                    disabled="@IsSubmitting">
                <i class="@GetButtonIcon(action)"></i>
                <span>@action</span>
            </button>
        }
    </div>
</div>

@code {
    [Parameter] public string Title { get; set; } = "Make a Decision";
    [Parameter] public string Subtitle { get; set; } = "";
    [Parameter] public IReadOnlyList<string> AvailableActions { get; set; } = [];
    [Parameter] public EventCallback<string> OnDecision { get; set; }
    [Parameter] public bool IsSubmitting { get; set; }
    
    private string GetButtonClass(string action) => action.ToUpperInvariant() switch
    {
        "DROP" => "drop-btn",
        "STAY" => "stay-btn",
        _ => "default-btn"
    };
    
    private string GetButtonIcon(string action) => action.ToUpperInvariant() switch
    {
        "DROP" => "fa-solid fa-door-open",
        "STAY" => "fa-solid fa-hand",
        _ => "fa-solid fa-circle"
    };
}
```

### 4.3 Create a Special Phase Overlay

**File: `CardGames.Poker.Web/Components/Shared/SpecialPhaseOverlay.razor`**

```razor
@namespace CardGames.Poker.Web.Components.Shared

<div class="table-overlay special-phase-overlay">
    <div class="overlay-content">
        <div class="overlay-icon"><i class="@IconClass"></i></div>
        <h2>@Title</h2>
        <p>@Description</p>
        @ChildContent
    </div>
</div>

@code {
    [Parameter] public string Title { get; set; } = "Special Phase";
    [Parameter] public string Description { get; set; } = "";
    [Parameter] public string IconClass { get; set; } = "fa-solid fa-star";
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

### 4.4 Update TablePlay.razor Overlay Logic

```razor
<!-- Main overlay logic based on phase category -->
@if (IsSetupPhase && !IsSeated)
{
    <WaitingOverlay Players="@seats" OnReady="@SetReadyAsync" />
}
else if (IsDecisionPhase && isPlayerTurn)
{
    <DecisionPanel 
        Title="@GetDecisionTitle()"
        Subtitle="@GetDecisionSubtitle()"
        AvailableActions="@GetCurrentPhaseActions()"
        OnDecision="@HandleDecisionAsync"
        IsSubmitting="@isSubmittingAction" />
}
else if (IsDrawingPhase && isPlayerDrawTurn)
{
    <DrawPanel 
        Cards="@currentPlayerDrawCards" 
        MaxDiscards="@(_tableState?.DrawingConfig?.MaxDiscards ?? 3)"
        ... />
}
else if (IsBettingPhase && isPlayerTurn)
{
    <ActionPanel 
        LegalActions="@legalActions"
        ... />
}
else if (IsResolutionPhase && _showShowdownOverlay)
{
    <ShowdownOverlay ShowdownResult="@_showdownResult" ... />
}
else if (IsSpecialPhase)
{
    <SpecialPhaseOverlay 
        Title="@_tableState?.CurrentPhaseDescription"
        Description="@GetSpecialPhaseDescription()" />
}
```

---

## Part 5: Handling Various Game Mechanics

### 5.1 Betting Rounds

The existing ActionPanel already handles betting well. Key changes:

1. **Remove game-specific routing:** Instead of routing to `FiveCardDrawApiClient` vs `TwosJacksManWithTheAxeApi`, create a unified endpoint or service.

2. **Use a universal game action endpoint:**

**New Endpoint: `POST /api/v1/games/{gameId}/action`**

```csharp
public record GameActionRequest(string ActionType, int? Amount, IReadOnlyList<int>? CardIndices);

// Handles: Bet, Call, Raise, Fold, Check, AllIn, Draw, Drop, Stay
```

### 5.2 Draw Mechanics

The DrawPanel component should be enhanced to support:

1. **Variable max discards:** Already supported via `MaxDiscards` parameter
2. **Special rules display:** Show Ace-bonus message when applicable
3. **Multiple draw rounds:** Track draw round number

```csharp
// Add to DrawPanel
[Parameter] public string? DrawingSpecialRules { get; set; }
[Parameter] public int CurrentDrawRound { get; set; } = 1;
[Parameter] public int TotalDrawRounds { get; set; } = 1;
```

### 5.3 Drop/Stay Mechanics (Kings and Lows)

Create a dedicated handler for the Decision phase:

```csharp
private async Task HandleDecisionAsync(string decision)
{
    if (isSubmittingAction)
        return;
    
    isSubmittingAction = true;
    try
    {
        var request = new GameActionRequest(decision, null, null);
        var response = await GamesApiClient.ProcessGameActionAsync(GameId, request);
        
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("Decision failed: {Error}", response.Error?.Content);
        }
        // SignalR will broadcast updated state
    }
    finally
    {
        isSubmittingAction = false;
    }
}
```

### 5.4 Pot Matching (Kings and Lows)

When the game has `HasPotMatching` special rule:

1. Display pot matching information in the resolution phase
2. Show who needs to match the pot
3. Display pot carry-over status

```razor
@if (_tableState?.SpecialRules?.HasPotMatching == true && _gameResponse?.CurrentPhase == "PotMatching")
{
    <PotMatchingOverlay 
        PotAmount="@pot"
        LosingPlayers="@GetLosingPlayers()"
        OnAcknowledge="@HandlePotMatchAcknowledge" />
}
```

### 5.5 Wild Cards Display

When the game has wild cards, highlight them in the UI. Instead of using hardcoded string pattern matching (which is difficult to maintain), use a structured approach with explicit wild card rules:

**Add structured wild card rules to SignalR DTOs:**

```csharp
/// <summary>
/// Defines which cards are wild in the current game.
/// </summary>
public sealed record WildCardRulesDto
{
    /// <summary>
    /// List of specific cards that are wild (e.g., "KD" for King of Diamonds).
    /// Format: "{Rank}{Suit}" where Rank is 2-10, J, Q, K, A and Suit is C, D, H, S.
    /// </summary>
    public IReadOnlyList<string>? SpecificCards { get; init; }
    
    /// <summary>
    /// List of ranks where all suits are wild (e.g., ["2", "J"] for all 2s and Jacks).
    /// </summary>
    public IReadOnlyList<string>? WildRanks { get; init; }
    
    /// <summary>
    /// Whether the player's lowest card is wild (for Kings and Lows).
    /// </summary>
    public bool LowestCardIsWild { get; init; }
    
    /// <summary>
    /// Human-readable description for UI display.
    /// </summary>
    public string? Description { get; init; }
}
```

**Update GameSpecialRulesDto:**

```csharp
public sealed record GameSpecialRulesDto
{
    // ... existing properties ...
    
    /// <summary>
    /// Structured wild card rules for the game.
    /// </summary>
    public WildCardRulesDto? WildCardRules { get; init; }
}
```

**Wild card detection logic:**

```csharp
// In CardInfo or card display logic
private bool IsWildCard(CardInfo card)
{
    var wildRules = _tableState?.SpecialRules?.WildCardRules;
    if (wildRules is null)
        return false;
    
    // Check specific cards (e.g., "KD" for King of Diamonds)
    if (wildRules.SpecificCards is not null)
    {
        var cardCode = $"{card.Rank}{GetSuitCode(card.Suit)}";
        if (wildRules.SpecificCards.Contains(cardCode, StringComparer.OrdinalIgnoreCase))
            return true;
    }
    
    // Check wild ranks (e.g., all 2s, all Jacks)
    if (wildRules.WildRanks is not null)
    {
        if (wildRules.WildRanks.Contains(card.Rank, StringComparer.OrdinalIgnoreCase))
            return true;
    }
    
    // Check if lowest card is wild (requires player's hand context)
    if (wildRules.LowestCardIsWild)
    {
        // Determined by private state - the API marks which cards are wild
        return card.IsWild;
    }
    
    return false;
}

private static string GetSuitCode(string? suit) => suit?.ToUpperInvariant() switch
{
    "CLUBS" => "C",
    "DIAMONDS" => "D",
    "HEARTS" => "H",
    "SPADES" => "S",
    _ => ""
};
```

**Build wild card rules in TableStateBuilder:**

```csharp
private static WildCardRulesDto? BuildWildCardRules(GameRules? rules)
{
    if (rules?.SpecialRules is null || !rules.SpecialRules.TryGetValue("WildCards", out var wildCardsValue))
        return null;
    
    var description = wildCardsValue?.ToString();
    
    // Parse known patterns into structured rules
    var wildRanks = new List<string>();
    var specificCards = new List<string>();
    var lowestCardIsWild = false;
    
    if (rules.GameTypeCode == "TWOSJACKSMANWITHTHEAXE")
    {
        wildRanks.AddRange(["2", "J"]);
        specificCards.Add("KD"); // King of Diamonds
    }
    else if (rules.GameTypeCode == "KINGSANDLOWS")
    {
        wildRanks.Add("K");
        lowestCardIsWild = true;
    }
    
    return new WildCardRulesDto
    {
        WildRanks = wildRanks.Count > 0 ? wildRanks : null,
        SpecificCards = specificCards.Count > 0 ? specificCards : null,
        LowestCardIsWild = lowestCardIsWild,
        Description = description
    };
}
```

This structured approach:
- Is explicit and easy to extend for new games
- Avoids fragile string pattern matching
- Allows the API to define wild card rules precisely
- Supports complex scenarios like "lowest card is wild"

---

## Implementation Roadmap

### Phase 1: Foundation (Days 1-2)

1. **Add phase metadata to SignalR DTOs**
   - [ ] Add `CurrentPhaseCategory` to `TableStatePublicDto`
   - [ ] Add `CurrentPhaseRequiresAction` to `TableStatePublicDto`
   - [ ] Add `CurrentPhaseAvailableActions` to `TableStatePublicDto`
   - [ ] Add `DrawingConfigDto` and `GameSpecialRulesDto`

2. **Update TableStateBuilder**
   - [ ] Populate phase metadata from game rules
   - [ ] Build special rules from game rules dictionary

### Phase 2: UI Helpers (Day 3)

1. **Create GameRulesService**
   - [ ] Implement caching for game rules
   - [ ] Register as singleton service

2. **Add helper properties to TablePlay.razor**
   - [ ] `IsBettingPhase`, `IsDrawingPhase`, `IsDecisionPhase`, etc.
   - [ ] `HasWildCards`, `HasSevensSplit`, `HasDropOrStay`

### Phase 3: Refactor TablePlay.razor (Days 4-5)

1. **Replace hardcoded phase checks**
   - [ ] Replace string comparisons with category-based checks
   - [ ] Remove game-specific conditionals

2. **Update overlay rendering**
   - [ ] Use category-based overlay selection
   - [ ] Create DecisionPanel component
   - [ ] Create SpecialPhaseOverlay component

### Phase 4: New Game Support (Day 6)

1. **Test with existing games**
   - [ ] Verify Five Card Draw works correctly
   - [ ] Verify Twos, Jacks, Man with the Axe works correctly
   - [ ] Verify Kings and Lows works correctly (if implemented)

2. **Add universal game action endpoint (optional)**
   - [ ] Create unified action handler
   - [ ] Route to appropriate game-specific logic

### Phase 5: Testing & Documentation (Day 7)

1. **Add unit tests**
   - [ ] Test phase category detection
   - [ ] Test special rules parsing

2. **Update documentation**
   - [ ] Update ARCHITECTURE.md with UI changes
   - [ ] Update ADDING_NEW_GAMES.md with UI considerations

---

## Summary

This plan transforms the TablePlay.razor from a hardcoded, game-specific implementation to a data-driven, game-agnostic UI that:

1. **Fetches game rules** on page load and uses them for all decisions
2. **Receives phase metadata** through SignalR in real-time
3. **Renders overlays** based on phase category, not phase ID
4. **Handles any game type** without code changes

The key insight is that **phase categories** (Setup, Betting, Drawing, Decision, Resolution, Special) are universal across all poker games, even if the specific phases vary. By driving the UI off categories and available actions, we achieve true game-agnosticism.

### Files to Create/Modify

**New Files:**
- `CardGames.Poker.Web/Services/GameRulesService.cs`
- `CardGames.Poker.Web/Components/Shared/DecisionPanel.razor`
- `CardGames.Poker.Web/Components/Shared/SpecialPhaseOverlay.razor`
- `CardGames.Poker.Web/Components/Shared/PotMatchingOverlay.razor` (for Kings and Lows)

**Modified Files:**
- `CardGames.Contracts/SignalR/TableStatePublicDto.cs` - Add phase metadata
- `CardGames.Contracts/SignalR/PrivateStateDto.cs` - Add decision state
- `CardGames.Poker.Api/Services/TableStateBuilder.cs` - Populate phase metadata
- `CardGames.Poker.Web/Components/Pages/TablePlay.razor` - Use category-based logic
- `CardGames.Poker.Web/Components/Shared/DrawPanel.razor` - Add special rules display
- `CardGames.Poker.Web/Program.cs` - Register GameRulesService

### Migration Strategy

1. **Add new properties without removing old ones** - Backward compatible
2. **Update UI to prefer new properties** - Falls back to old behavior if missing
3. **Gradually remove hardcoded checks** - After new properties are verified working
4. **Remove deprecated properties** - In a future release
