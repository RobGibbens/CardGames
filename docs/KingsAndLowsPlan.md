# Kings and Lows - Full Implementation Plan

## Overview

This plan outlines the complete implementation of Kings and Lows game into the CardGames system, including API, UI, SignalR, and database integration.

## Current State Analysis

### What Exists
- ✅ Domain layer: `KingsAndLowsGame.cs` with complete game logic
- ✅ Game rules: `KingsAndLowsRules.cs` with metadata
- ✅ Game metadata: Registered in `PokerGameMetadataRegistry`
- ✅ Rules registry: Registered in `PokerGameRulesRegistry`
- ✅ Phase enum: `KingsAndLowsPhase` with all phases defined
- ✅ Game tests: `KingsAndLowsGameTests.cs` exists

### What's Missing
- ❌ API Features: No API endpoints for creating/playing Kings and Lows games
- ❌ UI Integration: Not available in CreateTable dropdown
- ❌ SignalR Support: No game-specific SignalR handlers
- ❌ Database: No GameType seed data for Kings and Lows
- ❌ Client API: No Refitter-generated client for Kings and Lows

## Implementation Plan

### Phase 1: API Layer ✅ (Already Complete per IMPLEMENTATION_SUMMARY.md)
The game rules and metadata are already defined and registered.

### Phase 2: Create API Features (Commands & Queries)

#### 2.1 Create Game Command
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/CreateGame/`

Files to create:
- `CreateGameCommand.cs` - Command definition
- `CreateGameCommandHandler.cs` - Handles game creation
- `CreateGameCommandValidator.cs` - Validates command
- `CreateGameEndpoint.cs` - HTTP endpoint

**Endpoint**: `POST /api/v1/games/kingsandlows`

**Request**:
```json
{
  "gameId": "01936e5a-...",
  "tableName": "Friday Night Poker",
  "ante": 5,
  "minBet": 10,
  "players": [
    {"name": "Alice", "startingChips": 1000},
    {"name": "Bob", "startingChips": 1000}
  ]
}
```

#### 2.2 Start Hand Command
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/StartHand/`

Files to create:
- `StartHandCommand.cs`
- `StartHandCommandHandler.cs`
- `StartHandEndpoint.cs`

**Endpoint**: `POST /api/v1/games/kingsandlows/{gameId}/start-hand`

#### 2.3 Drop or Stay Command
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/DropOrStay/`

Files to create:
- `DropOrStayCommand.cs`
- `DropOrStayCommandHandler.cs`
- `DropOrStayEndpoint.cs`

**Endpoint**: `POST /api/v1/games/kingsandlows/{gameId}/drop-or-stay`

**Request**:
```json
{
  "playerId": "01936e5a-...",
  "decision": "Stay" // or "Drop"
}
```

#### 2.4 Draw Cards Command
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/DrawCards/`

Files to create:
- `DrawCardsCommand.cs`
- `DrawCardsCommandHandler.cs`
- `DrawCardsEndpoint.cs`

**Endpoint**: `POST /api/v1/games/kingsandlows/{gameId}/draw`

**Request**:
```json
{
  "playerId": "01936e5a-...",
  "discardIndices": [0, 2, 4] // Cards to discard
}
```

#### 2.5 Deck Draw Command (Player vs Deck scenario)
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/DeckDraw/`

Files to create:
- `DeckDrawCommand.cs`
- `DeckDrawCommandHandler.cs`
- `DeckDrawEndpoint.cs`

**Endpoint**: `POST /api/v1/games/kingsandlows/{gameId}/deck-draw`

**Request**:
```json
{
  "discardIndices": [1, 3] // Deck cards to discard (dealer decides)
}
```

#### 2.6 Acknowledge Pot Matching Command
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/AcknowledgePotMatch/`

Files to create:
- `AcknowledgePotMatchCommand.cs`
- `AcknowledgePotMatchCommandHandler.cs`
- `AcknowledgePotMatchEndpoint.cs`

**Endpoint**: `POST /api/v1/games/kingsandlows/{gameId}/acknowledge-pot-match`

#### 2.7 Register Endpoints
**File**: `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/V1.cs`

Create endpoint registration:
```csharp
public static class V1
{
    public static IEndpointRouteBuilder MapKingsAndLowsV1(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/games/kingsandlows")
            .WithTags("Kings and Lows");

        group.MapCreateGameEndpoint();
        group.MapStartHandEndpoint();
        group.MapDropOrStayEndpoint();
        group.MapDrawCardsEndpoint();
        group.MapDeckDrawEndpoint();
        group.MapAcknowledgePotMatchEndpoint();

        return endpoints;
    }
}
```

And register in `Program.cs`:
```csharp
app.MapKingsAndLowsV1();
```

### Phase 3: Game State Management

#### 3.1 Create Game State Repository
**File**: `src/CardGames.Poker.Api/Repositories/KingsAndLowsGameRepository.cs`

Handles:
- Storing active games in memory (or cache)
- Retrieving games by ID
- Managing game lifecycle

#### 3.2 Update TableStateBuilder
**File**: `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

Ensure `TableStateBuilder` can handle Kings and Lows specific state:
- Current phase (DropOrStay, PlayerVsDeck, PotMatching)
- Player decisions (who has dropped, who has stayed)
- Deck hand (when in PlayerVsDeck phase)
- Pot matching amounts

### Phase 4: SignalR Integration

#### 4.1 Extend SignalR DTOs
**Files to modify**:
- `src/CardGames.Contracts/SignalR/TableStatePublicDto.cs`
- `src/CardGames.Contracts/SignalR/PrivateStateDto.cs`

Add Kings and Lows specific fields:

**TableStatePublicDto additions**:
```csharp
/// <summary>
/// Kings and Lows specific: Players who have dropped
/// </summary>
public IReadOnlyList<string>? DroppedPlayerIds { get; init; }

/// <summary>
/// Kings and Lows specific: Deck's hand in player vs deck scenario
/// </summary>
public IReadOnlyList<CardInfo>? DeckHand { get; init; }

/// <summary>
/// Kings and Lows specific: Losers who need to match pot
/// </summary>
public IReadOnlyList<string>? LosersToMatchPot { get; init; }

/// <summary>
/// Kings and Lows specific: Amounts each loser needs to match
/// </summary>
public IReadOnlyDictionary<string, int>? PotMatchAmounts { get; init; }
```

**PrivateStateDto additions**:
```csharp
/// <summary>
/// Kings and Lows specific: Whether it's my turn to decide drop/stay
/// </summary>
public bool IsMyTurnToDecideDropOrStay { get; init; }

/// <summary>
/// Kings and Lows specific: My decision status
/// </summary>
public string? MyDropOrStayDecision { get; init; } // "Undecided", "Dropped", "Stayed"

/// <summary>
/// Kings and Lows specific: Wild cards in my hand
/// </summary>
public IReadOnlyList<int>? MyWildCardIndices { get; init; }
```

#### 4.2 Update GameHub
**File**: `src/CardGames.Poker.Api/Hubs/GameHub.cs`

Add methods for Kings and Lows actions:
- `DropOrStayAsync(Guid gameId, string decision)`
- `DrawCardsAsync(Guid gameId, int[] discardIndices)`
- `DeckDrawAsync(Guid gameId, int[] discardIndices)`

### Phase 5: UI Integration

#### 5.1 Update CreateTable Component
**File**: `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor`

**Changes needed**:

1. Import Kings and Lows API client:
```csharp
@inject IKingsAndLowsApi KingsAndLowsApi
```

2. Update `IsGameAvailable` method:
```csharp
private static bool IsGameAvailable(string gameName)
{
    return gameName is 
        "Five Card Draw" or 
        "Twos, Jacks, Man with the Axe" or
        "Kings and Lows";
}
```

3. Update `CreateTableAsync` method to handle Kings and Lows:
```csharp
else if (selectedVariant.Name == "Kings and Lows")
{
    var response = await KingsAndLowsApi.KingsAndLowsCreateGameAsync(command);
    if (response.IsSuccessStatusCode && response.Content != Guid.Empty)
    {
        createdGameId = response.Content;
    }
}
```

#### 5.2 Create Drop/Stay Overlay Component
**File**: `src/CardGames.Poker.Web/Components/Shared/DropOrStayOverlay.razor`

A new overlay component for the drop-or-stay decision phase:

```razor
<div class="table-overlay drop-or-stay-overlay">
    <div class="overlay-content">
        <div class="overlay-icon"><i class="fa-solid fa-door-open"></i></div>
        <h2>Drop or Stay?</h2>
        <p>Decide whether to stay in this hand or drop out</p>
        
        <div class="decision-buttons">
            <button class="btn btn-danger drop-btn" @onclick="OnDrop" disabled="@IsSubmitting">
                <i class="fa-solid fa-door-open"></i>
                <span>Drop</span>
            </button>
            <button class="btn btn-success stay-btn" @onclick="OnStay" disabled="@IsSubmitting">
                <i class="fa-solid fa-hand"></i>
                <span>Stay</span>
            </button>
        </div>
    </div>
</div>

@code {
    [Parameter] public EventCallback OnDrop { get; set; }
    [Parameter] public EventCallback OnStay { get; set; }
    [Parameter] public bool IsSubmitting { get; set; }
}
```

#### 5.3 Create Player vs Deck Overlay Component
**File**: `src/CardGames.Poker.Web/Components/Shared/PlayerVsDeckOverlay.razor`

Shows the deck's hand and allows dealer to choose discards:

```razor
<div class="player-vs-deck-overlay">
    <h3>Player vs Deck</h3>
    <p>Only one player stayed. They're playing against the deck!</p>
    
    <div class="deck-hand">
        <h4>Deck's Hand</h4>
        <div class="card-row">
            @foreach (var card in DeckHand)
            {
                <TableCard Card="@card" />
            }
        </div>
    </div>
    
    @if (ShowDeckDrawControls)
    {
        <div class="deck-draw-controls">
            <p>Dealer, select cards to discard for the deck:</p>
            <button class="btn btn-primary" @onclick="OnDeckDraw">Confirm Discards</button>
        </div>
    }
</div>

@code {
    [Parameter] public IReadOnlyList<CardInfo> DeckHand { get; set; } = [];
    [Parameter] public bool ShowDeckDrawControls { get; set; }
    [Parameter] public EventCallback OnDeckDraw { get; set; }
}
```

#### 5.4 Create Pot Matching Overlay Component
**File**: `src/CardGames.Poker.Web/Components/Shared/PotMatchingOverlay.razor`

Shows losers and pot matching amounts:

```razor
<div class="table-overlay pot-matching-overlay">
    <div class="overlay-content">
        <div class="overlay-icon"><i class="fa-solid fa-coins"></i></div>
        <h2>Pot Matching</h2>
        <p>The following players must match the pot:</p>
        
        <div class="losers-list">
            @foreach (var (loser, amount) in PotMatchAmounts)
            {
                <div class="loser-row">
                    <span class="loser-name">@loser</span>
                    <span class="match-amount">@amount chips</span>
                </div>
            }
        </div>
        
        <button class="btn btn-primary" @onclick="OnAcknowledge">Continue</button>
    </div>
</div>

@code {
    [Parameter] public IReadOnlyDictionary<string, int> PotMatchAmounts { get; set; } = new Dictionary<string, int>();
    [Parameter] public EventCallback OnAcknowledge { get; set; }
}
```

#### 5.5 Update TablePlay Component
**File**: `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`

**Changes needed**:

1. Add Kings and Lows API client injection:
```csharp
@inject IKingsAndLowsApi KingsAndLowsApi
```

2. Add overlay rendering logic:
```razor
@* Kings and Lows: Drop or Stay Overlay *@
@if (IsKingsAndLows && currentPhase == "DropOrStay" && isMyTurnToDecide)
{
    <DropOrStayOverlay OnDrop="HandleDropAsync" 
                       OnStay="HandleStayAsync" 
                       IsSubmitting="@isSubmittingAction" />
}

@* Kings and Lows: Player vs Deck Overlay *@
@if (IsKingsAndLows && currentPhase == "PlayerVsDeck" && _tableState?.DeckHand is not null)
{
    <PlayerVsDeckOverlay DeckHand="@_tableState.DeckHand" 
                        ShowDeckDrawControls="@isDealerAndCanDrawForDeck"
                        OnDeckDraw="HandleDeckDrawAsync" />
}

@* Kings and Lows: Pot Matching Overlay *@
@if (IsKingsAndLows && currentPhase == "PotMatching" && _tableState?.PotMatchAmounts is not null)
{
    <PotMatchingOverlay PotMatchAmounts="@_tableState.PotMatchAmounts"
                       OnAcknowledge="HandlePotMatchAcknowledgeAsync" />
}
```

3. Add action handlers:
```csharp
private bool IsKingsAndLows => _gameTypeCode?.Equals("KINGSANDLOWS", StringComparison.OrdinalIgnoreCase) == true;

private async Task HandleDropAsync()
{
    if (!IsKingsAndLows || string.IsNullOrEmpty(_myPlayerId)) return;
    
    isSubmittingAction = true;
    try
    {
        var command = new DropOrStayCommand(GameId, _myPlayerId, "Drop");
        await KingsAndLowsApi.KingsAndLowsDropOrStayAsync(command);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to submit drop decision");
    }
    finally
    {
        isSubmittingAction = false;
    }
}

private async Task HandleStayAsync()
{
    if (!IsKingsAndLows || string.IsNullOrEmpty(_myPlayerId)) return;
    
    isSubmittingAction = true;
    try
    {
        var command = new DropOrStayCommand(GameId, _myPlayerId, "Stay");
        await KingsAndLowsApi.KingsAndLowsDropOrStayAsync(command);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to submit stay decision");
    }
    finally
    {
        isSubmittingAction = false;
    }
}

private async Task HandleDeckDrawAsync()
{
    // Get selected discard indices from UI state
    var discardIndices = GetSelectedDeckDiscardIndices();
    
    isSubmittingAction = true;
    try
    {
        var command = new DeckDrawCommand(GameId, discardIndices);
        await KingsAndLowsApi.KingsAndLowsDeckDrawAsync(command);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to process deck draw");
    }
    finally
    {
        isSubmittingAction = false;
    }
}

private async Task HandlePotMatchAcknowledgeAsync()
{
    isSubmittingAction = true;
    try
    {
        var command = new AcknowledgePotMatchCommand(GameId);
        await KingsAndLowsApi.KingsAndLowsAcknowledgePotMatchAsync(command);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to acknowledge pot match");
    }
    finally
    {
        isSubmittingAction = false;
    }
}
```

4. Wild card highlighting:
```csharp
private bool IsWildCard(CardInfo card, int cardIndex)
{
    if (!IsKingsAndLows) return false;
    
    // Check if card is a King (always wild)
    if (card.Rank == "K") return true;
    
    // Check if card is the lowest in hand (from server state)
    if (_privateState?.MyWildCardIndices?.Contains(cardIndex) == true)
        return true;
    
    return false;
}
```

### Phase 6: Database Setup

#### 6.1 Create Migration for Kings and Lows GameType
**File**: `src/CardGames.Poker.Api/Migrations/YYYYMMDDHHMMSS_AddKingsAndLowsGameType.cs`

Create a migration that seeds the Kings and Lows game type:

```csharp
public partial class AddKingsAndLowsGameType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var kingsAndLowsId = Guid.Parse("01936e5a-1234-7890-abcd-000000000003"); // Use a fixed V7 GUID
        
        migrationBuilder.InsertData(
            table: "GameTypes",
            columns: new[] { 
                "Id", "Name", "Description", "Code", "BettingStructure", 
                "MinPlayers", "MaxPlayers", "InitialHoleCards", "InitialBoardCards",
                "MaxCommunityCards", "MaxPlayerCards", "HasDrawPhase", "MaxDiscards",
                "WildCardRule", "IsActive", "CreatedAt", "UpdatedAt"
            },
            values: new object[] {
                kingsAndLowsId,
                "Kings and Lows",
                "A five-card draw poker variant where kings and the lowest card are wild. Players ante, decide to drop or stay, draw cards, and losers match the pot.",
                "KINGSANDLOWS",
                3, // AntePotMatch
                2, // MinPlayers
                5, // MaxPlayers
                5, // InitialHoleCards
                0, // InitialBoardCards
                0, // MaxCommunityCards
                5, // MaxPlayerCards
                true, // HasDrawPhase
                5, // MaxDiscards
                3, // LowestCard
                true, // IsActive
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            }
        );
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "GameTypes",
            keyColumn: "Code",
            keyValue: "KINGSANDLOWS"
        );
    }
}
```

Run migration:
```bash
dotnet ef migrations add AddKingsAndLowsGameType --project src/CardGames.Poker.Api
dotnet ef database update --project src/CardGames.Poker.Api
```

### Phase 7: Refitter Client Generation

#### 7.1 Update Refitter Configuration
**File**: `src/CardGames.Poker.Api.Clients/refitter.settings.json`

Add Kings and Lows endpoints to the API specification.

#### 7.2 Generate Client
Run Refitter to generate the `IKingsAndLowsApi` client interface:
```bash
cd src/CardGames.Poker.Api.Clients
dotnet build
```

This should generate the client interface automatically if Refitter is set up correctly.

### Phase 8: Testing

#### 8.1 Unit Tests
**Files to create/update**:
- `src/Tests/CardGames.Poker.Api.Tests/Features/Games/KingsAndLows/CreateGameCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Api.Tests/Features/Games/KingsAndLows/DropOrStayCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Api.Tests/Features/Games/KingsAndLows/DrawCardsCommandHandlerTests.cs`

#### 8.2 Integration Tests
Test the full game flow:
1. Create game
2. Start hand
3. Collect antes
4. Deal cards
5. Drop/stay decisions
6. Draw phase
7. Player vs deck (if only one stays)
8. Showdown
9. Pot matching
10. Complete

#### 8.3 UI Testing
Manual testing checklist:
- [ ] Can create a Kings and Lows table from UI
- [ ] Can see drop/stay overlay when it's my turn
- [ ] Can drop or stay successfully
- [ ] Can draw cards (up to 5)
- [ ] Can see wild cards highlighted (Kings + lowest card)
- [ ] Player vs deck scenario works correctly
- [ ] Pot matching overlay appears for losers
- [ ] Can start a new round after pot matching
- [ ] SignalR updates all players in real-time

### Phase 9: Documentation

#### 9.1 Update ADDING_NEW_GAMES.md
Add Kings and Lows as a complete example.

#### 9.2 Update README.md
Add Kings and Lows to the list of supported games.

#### 9.3 Create Kings and Lows Gameplay Guide
**File**: `docs/games/KingsAndLows.md`

Document the rules and special mechanics.

## Implementation Checklist

### Domain Layer (Already Complete)
- [x] KingsAndLowsGame.cs - Game logic
- [x] KingsAndLowsRules.cs - Game rules metadata
- [x] KingsAndLowsPhase.cs - Phase enum
- [x] KingsAndLowsGamePlayer.cs - Player state
- [x] KingsAndLowsShowdownResult.cs - Showdown result
- [x] Registered in PokerGameMetadataRegistry
- [x] Registered in PokerGameRulesRegistry

### API Layer
- [ ] Create API feature directory structure
- [ ] CreateGameCommand + Handler + Endpoint
- [ ] StartHandCommand + Handler + Endpoint
- [ ] DropOrStayCommand + Handler + Endpoint
- [ ] DrawCardsCommand + Handler + Endpoint
- [ ] DeckDrawCommand + Handler + Endpoint
- [ ] AcknowledgePotMatchCommand + Handler + Endpoint
- [ ] V1.cs endpoint registration
- [ ] Update Program.cs to map endpoints
- [ ] KingsAndLowsGameRepository for state management
- [ ] Update TableStateBuilder for Kings and Lows

### SignalR
- [ ] Extend TableStatePublicDto
- [ ] Extend PrivateStateDto
- [ ] Update GameHub with Kings and Lows methods
- [ ] Test SignalR broadcasts

### Database
- [ ] Create migration for GameType seed data
- [ ] Run migration
- [ ] Verify GameType in database

### UI Layer
- [ ] Update CreateTable.razor
  - [ ] Add IKingsAndLowsApi injection
  - [ ] Update IsGameAvailable
  - [ ] Update CreateTableAsync
- [ ] Create DropOrStayOverlay.razor
- [ ] Create PlayerVsDeckOverlay.razor
- [ ] Create PotMatchingOverlay.razor
- [ ] Update TablePlay.razor
  - [ ] Add IKingsAndLowsApi injection
  - [ ] Add overlay rendering
  - [ ] Add action handlers
  - [ ] Add wild card highlighting
- [ ] Create CSS for new overlays

### Client Generation
- [ ] Update Refitter configuration
- [ ] Generate IKingsAndLowsApi client
- [ ] Verify client methods

### Testing
- [ ] Unit tests for command handlers
- [ ] Integration tests for game flow
- [ ] UI manual testing
- [ ] SignalR testing

### Documentation
- [ ] Update ADDING_NEW_GAMES.md
- [ ] Update README.md
- [ ] Create gameplay guide

## Special Considerations

### Drop/Stay Decision Flow
- All players make their decision simultaneously (no turn order)
- UI should show who has decided and who is still deciding
- Phase only advances when all players have decided

### Player vs Deck Scenario
- When only one player stays, deal a separate hand for the "deck"
- Dealer (or player to dealer's left if dealer stayed) chooses discards for deck
- Deck hand is visible to all players
- If player wins, they get the pot and hand ends
- If deck wins, player matches pot and new hand begins

### Pot Matching
- After showdown, losers must match the pot amount
- If a loser doesn't have enough chips, they go all-in
- The matched amounts form the pot for the next hand
- Pot carries over across multiple hands until someone wins it all

### Wild Cards
- All Kings are always wild
- Each player's lowest card is also wild
- Wild cards are determined server-side and sent to each player privately
- UI should highlight wild cards visually

### Continuous Play
- Unlike traditional poker, a "game" of Kings and Lows consists of multiple hands
- The game continues until only one player has chips
- Each hand (pot) is a round within the larger game

## Risk Mitigation

### Complexity of Multi-Hand Game Flow
**Risk**: The game flow is more complex than Five Card Draw (multiple hands, pot carrying over)
**Mitigation**: 
- Model the state machine carefully in the domain layer
- Use clear phase transitions
- Test edge cases thoroughly (all drop, single player stays, etc.)

### Wild Card Calculation
**Risk**: Determining "lowest card" is player-specific and must be calculated server-side
**Mitigation**:
- Use existing `WildCardRules.DetermineWildCards()` method
- Send wild card indices in private state to each player
- Don't expose other players' wild cards

### SignalR State Synchronization
**Risk**: Multiple overlays and complex state could cause sync issues
**Mitigation**:
- Always broadcast complete state after each action
- Use optimistic UI updates with rollback on error
- Test with multiple simultaneous players

### UI Responsiveness
**Risk**: Multiple overlays could be confusing or slow
**Mitigation**:
- Use consistent overlay patterns
- Add smooth transitions
- Show clear status indicators
- Test on various screen sizes

## Success Criteria

The implementation is complete when:
1. ✅ Can create a Kings and Lows table from the UI
2. ✅ Can play a complete hand from ante to pot matching
3. ✅ Player vs deck scenario works correctly
4. ✅ Wild cards are correctly identified and highlighted
5. ✅ Pot carries over across multiple hands
6. ✅ All players see real-time updates via SignalR
7. ✅ Database correctly stores game state
8. ✅ Tests pass for all commands and scenarios
9. ✅ Documentation is complete and accurate

## Timeline Estimate

- Phase 1: API Commands & Handlers - 4 hours
- Phase 2: SignalR Integration - 2 hours
- Phase 3: Database Migration - 1 hour
- Phase 4: UI Components - 4 hours
- Phase 5: Client Generation - 1 hour
- Phase 6: Testing - 3 hours
- Phase 7: Documentation - 1 hour

**Total**: ~16 hours of development time

## Notes

- This implementation follows the game-agnostic architecture principles
- The game rules metadata is already complete and provides UI guidance
- Most of the complexity is in the API layer; UI follows established patterns
- The drop/stay overlay is similar to the betting action panel
- Player vs deck is a unique feature that needs special UI treatment
