# Game-Agnostic Architecture Design

## Overview

This document describes the new extensible architecture for the Card Games poker system that allows multiple game types to coexist without hardcoding game-specific logic in the UI or API layer.

## Problem Statement

The original architecture had several issues:

1. **Tight Coupling**: The UI (TablePlay.razor) and API handlers contained hardcoded game-specific logic with conditionals like `if (game == FiveCardDraw) {} else if (game == TwosJacksManWithTheAxe) {}`
2. **Limited Scalability**: Adding a new game type required changes across multiple layers (domain, API, contracts, and UI)
3. **Game Flow Assumptions**: The system assumed all games follow a Five Card Draw pattern (antes, deal 5 face-down cards, draw, two betting rounds, showdown)
4. **Duplication**: Similar logic was duplicated across different game implementations

## Solution: Rule-Driven Architecture

The new architecture introduces a **metadata-driven system** where:

1. **Game rules live in the domain layer** as structured metadata
2. **The API exposes these rules** via REST endpoints
3. **The UI adapts dynamically** based on the game rules received from the API
4. **No game-specific conditionals** are needed in the UI or API orchestration code

## Core Components

### 1. Domain Layer (CardGames.Poker)

#### GameRules Class
A comprehensive metadata descriptor that defines all aspects of a game variant:

```csharp
public class GameRules
{
    public string GameTypeCode { get; init; }
    public string GameTypeName { get; init; }
    public int MinPlayers { get; init; }
    public int MaxPlayers { get; init; }
    
    // Ordered phases that define the game flow
    public IReadOnlyList<GamePhaseDescriptor> Phases { get; init; }
    
    // Configuration objects
    public CardDealingConfig CardDealing { get; init; }
    public BettingConfig Betting { get; init; }
    public DrawingConfig? Drawing { get; init; }
    public ShowdownConfig Showdown { get; init; }
    
    // Extensibility for game-specific rules
    public IReadOnlyDictionary<string, object>? SpecialRules { get; init; }
}
```

#### Key Abstractions

- **IGamePhase**: Interface for representing a phase in the game lifecycle
- **IPlayerAction**: Interface for representing actions players can take
- **GamePhaseDescriptor**: Describes a phase with metadata (ID, name, category, required actions)

#### IPokerGame Extension
All game implementations now provide their rules:

```csharp
public interface IPokerGame
{
    string Name { get; }
    string Description { get; }
    int MinimumNumberOfPlayers { get; }
    int MaximumNumberOfPlayers { get; }
    
    // NEW: Metadata-driven game rules
    GameRules GetGameRules();
}
```

### 2. API Layer (CardGames.Poker.Api)

#### PokerGameRulesRegistry
Centralized registry that maps game type codes to their rule factories:

```csharp
public static class PokerGameRulesRegistry
{
    public static bool TryGet(string? gameTypeCode, out GameRules? rules);
    public static GameRules Get(string gameTypeCode);
    public static IEnumerable<string> GetAvailableGameTypeCodes();
}
```

#### New Endpoint
`GET /api/v1/games/rules/{gameTypeCode}` returns the game rules as JSON.

Example response for Five Card Draw:
```json
{
  "gameTypeCode": "FIVECARDDRAW",
  "gameTypeName": "Five Card Draw",
  "phases": [
    {
      "phaseId": "WaitingToStart",
      "name": "Waiting to Start",
      "category": "Setup",
      "requiresPlayerAction": false
    },
    {
      "phaseId": "FirstBettingRound",
      "name": "First Betting Round",
      "category": "Betting",
      "requiresPlayerAction": true,
      "availableActions": ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
    },
    {
      "phaseId": "DrawPhase",
      "name": "Draw Phase",
      "category": "Drawing",
      "requiresPlayerAction": true,
      "availableActions": ["Draw"]
    }
    // ... more phases
  ],
  "cardDealing": {
    "initialCards": 5,
    "initialVisibility": "FaceDown",
    "hasCommunityCards": false
  },
  "betting": {
    "hasAntes": true,
    "hasBlinds": false,
    "bettingRounds": 2,
    "structure": "Fixed Limit"
  }
  // ... more config
}
```

### 3. Contract Layer (CardGames.Contracts)

#### GameRulesDto
DTOs mirror the domain GameRules structure but are optimized for serialization and API contracts. See `GameRulesDto.cs` for the complete structure.

## Usage Examples

### Adding a New Game Type

To add a new game (e.g., "Seven Card Stud"):

1. **Implement the domain game class** as usual (e.g., `SevenCardStudGame.cs`)

2. **Create a rules factory** (e.g., `SevenCardStudRules.cs`):

```csharp
public static class SevenCardStudRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "SEVENCARDSTUD",
            GameTypeName = "Seven Card Stud",
            Phases = new List<GamePhaseDescriptor>
            {
                new() { 
                    PhaseId = "CollectingAntes", 
                    Name = "Collecting Antes",
                    Category = "Setup"
                },
                new() { 
                    PhaseId = "DealingTwoDown", 
                    Name = "Dealing Two Down",
                    Category = "Dealing"
                },
                // ... define all phases
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 2,
                InitialVisibility = CardVisibility.FaceDown,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    // ... more rounds
                }
            },
            Betting = new BettingConfig
            {
                HasAntes = true,
                BettingRounds = 5,
                Structure = "Fixed Limit"
            }
        };
    }
}
```

3. **Register in PokerGameRulesRegistry**:

```csharp
private static readonly FrozenDictionary<string, Func<GameRules>> RulesByGameTypeCode =
    new Dictionary<string, Func<GameRules>>(StringComparer.OrdinalIgnoreCase)
    {
        [PokerGameMetadataRegistry.SevenCardStudCode] = SevenCardStudRules.CreateGameRules,
        // ... other games
    }.ToFrozenDictionary();
```

**That's it!** No UI changes required. The UI will automatically:
- Display the correct phases
- Show appropriate action buttons based on `availableActions`
- Adapt the table display based on `cardDealing` configuration

### UI Consumption (Future Implementation)

The UI layer will query game rules on table load:

```csharp
// Fetch rules for current game
var rulesResponse = await GamesApiClient.GetGameRulesAsync(gameTypeCode);
var rules = rulesResponse.Rules;

// Render phases dynamically
foreach (var phase in rules.Phases)
{
    if (phase.RequiresPlayerAction)
    {
        // Show action UI based on phase.AvailableActions
        foreach (var action in phase.AvailableActions)
        {
            // Render button/control for this action
        }
    }
}

// Configure card display
if (rules.CardDealing.HasCommunityCards)
{
    // Show community card area
}
```

## Benefits of This Architecture

1. **Extensibility**: Add new games without touching UI code
2. **Maintainability**: Game logic is centralized and declarative
3. **Consistency**: All games follow the same metadata structure
4. **Testability**: Rules can be unit tested independently
5. **Documentation**: The rules serve as machine-readable game documentation
6. **Flexibility**: Special rules can be added via the dictionary without breaking the schema

## Migration Path

This is a **non-breaking change**. Existing games continue to work as before. The new architecture provides:

1. **Backward compatibility**: Current game implementations are not affected
2. **Gradual adoption**: UI can be refactored incrementally to use rules
3. **Coexistence**: Old and new approaches work side-by-side during transition

## Future Enhancements

Potential extensions to this architecture:

1. **Dynamic action validation**: Rules could include action validation logic
2. **Phase transition rules**: Define valid phase transitions in metadata
3. **UI component hints**: Suggest which UI components to render for each phase
4. **Internationalization**: Rules include translated names/descriptions
5. **Client-side game simulation**: Rules enable offline/practice modes
6. **Tournament configurations**: Extend rules to support tournament structures

## Comparison: Before vs. After

### Before (Hardcoded)
```csharp
// In UI layer
if (gameTypeCode == "FIVECARDDRAW")
{
    if (currentPhase == "DrawPhase")
    {
        // Show draw UI with 3-card limit
        ShowDrawUI(maxCards: 3);
    }
}
else if (gameTypeCode == "TWOSJACKSMANWITHTHEAXE")
{
    if (currentPhase == "DrawPhase")
    {
        // Show draw UI with wild card indicators
        ShowDrawUI(maxCards: 3, showWildCards: true);
    }
}
```

### After (Rule-Driven)
```csharp
// In UI layer
var phase = rules.Phases.FirstOrDefault(p => p.PhaseId == currentPhase);
if (phase?.Category == "Drawing" && phase.RequiresPlayerAction)
{
    // Rules tell us everything we need
    var maxCards = rules.Drawing.MaxDiscards ?? 5;
    var hasWildCards = rules.SpecialRules?.ContainsKey("WildCards") ?? false;
    
    ShowDrawUI(maxCards, hasWildCards);
}
```

## Conclusion

This architecture transforms the card games system from a hardcoded, game-specific implementation to a flexible, metadata-driven platform. New game types can be added by simply defining their rules in the domain layer, with no changes required to the UI or API orchestration logic.

The system remains backward compatible while providing a clear path forward for scaling to dozens or hundreds of game variants without increasing code complexity.
