# Adding a New Game Type: Developer Guide

This guide walks you through adding a new poker variant to the system using the game-agnostic architecture.

## Overview

Adding a new game type involves:
1. Implementing the game logic in the domain layer
2. Defining the game rules metadata
3. Registering the game in the API layer

**No UI changes are required** - the UI adapts automatically based on the game rules.

## Step-by-Step Example: Adding "Kings and Lows"

Let's walk through adding the "Kings and Lows" game as an example.

### Step 1: Implement the Game Logic

Create the game implementation in `CardGames.Poker/Games/YourGameName/`:

```csharp
// KingsAndLowsGame.cs
namespace CardGames.Poker.Games.KingsAndLows;

[PokerGameMetadata(
    "Kings and Lows",
    "A five-card draw variant where kings and the lowest card are wild.",
    2,
    5,
    "kingsandlows.png")]
public class KingsAndLowsGame : IPokerGame
{
    public string Name { get; } = "Kings and Lows";
    public string Description { get; } = "...";
    public int MinimumNumberOfPlayers { get; } = 2;
    public int MaximumNumberOfPlayers { get; } = 5;

    // Implement game logic methods
    public void StartHand() { /* ... */ }
    public void DealHands() { /* ... */ }
    // ... other game methods

    // NEW: Implement GetGameRules()
    public GameRules GetGameRules()
    {
        return KingsAndLowsRules.CreateGameRules();
    }
}
```

### Step 2: Define Game Phases

Create an enum for your game's phases:

```csharp
// KingsAndLowsPhase.cs
public enum KingsAndLowsPhase
{
    WaitingToStart,
    CollectingAntes,
    Dealing,
    DropOrStay,
    DrawPhase,
    PlayerVsDeck,
    Showdown,
    PotMatching,
    Complete
}
```

### Step 3: Create the Game Rules

Create a rules factory class:

```csharp
// KingsAndLowsRules.cs
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.KingsAndLows;

public static class KingsAndLowsRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "KINGSANDLOWS",
            GameTypeName = "Kings and Lows",
            Description = "A five-card draw poker variant where kings and the lowest card are wild. Players ante, decide to drop or stay, draw cards, and losers match the pot.",
            MinPlayers = 2,
            MaxPlayers = 5,
            
            // Define phases in order
            Phases = new List<GamePhaseDescriptor>
            {
                new() 
                { 
                    PhaseId = "WaitingToStart", 
                    Name = "Waiting to Start", 
                    Description = "Waiting for players to join and ready up",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "CollectingAntes", 
                    Name = "Collecting Antes", 
                    Description = "Collecting ante bets from all players",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "Dealing", 
                    Name = "Dealing", 
                    Description = "Dealing 5 cards to each player",
                    Category = "Dealing",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "DropOrStay", 
                    Name = "Drop or Stay", 
                    Description = "Players decide whether to drop out or stay in the hand",
                    Category = "Decision",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Drop", "Stay" }
                },
                new() 
                { 
                    PhaseId = "DrawPhase", 
                    Name = "Draw Phase", 
                    Description = "Players discard and draw replacement cards",
                    Category = "Drawing",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Draw" }
                },
                new() 
                { 
                    PhaseId = "Showdown", 
                    Name = "Showdown", 
                    Description = "Players reveal hands and winner is determined",
                    Category = "Resolution",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "Complete", 
                    Name = "Complete", 
                    Description = "Hand is complete",
                    Category = "Resolution",
                    RequiresPlayerAction = false,
                    IsTerminal = true
                }
            },
            
            // Configure card dealing
            CardDealing = new CardDealingConfig
            {
                InitialCards = 5,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = false
            },
            
            // Configure betting
            Betting = new BettingConfig
            {
                HasAntes = true,
                HasBlinds = false,
                BettingRounds = 0,  // No traditional betting
                Structure = "No Betting"
            },
            
            // Configure drawing
            Drawing = new DrawingConfig
            {
                AllowsDrawing = true,
                MaxDiscards = 5,
                SpecialRules = "Players can discard all 5 cards",
                DrawingRounds = 1
            },
            
            // Configure showdown
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard Poker (High) with Wild Cards",
                IsHighLow = false,
                HasSpecialSplitRules = true,
                SpecialSplitDescription = "Losers match the pot; pot carries over until someone wins it all"
            },
            
            // Special rules (game-specific features)
            SpecialRules = new Dictionary<string, object>
            {
                ["WildCards"] = "All Kings and each player's lowest card",
                ["DropOrStay"] = true,
                ["PotCarryOver"] = true,
                ["LosersMatchPot"] = true
            }
        };
    }
}
```

### Step 4: Register in the API

Add your game to the registry in `PokerGameRulesRegistry.cs`:

```csharp
private static readonly FrozenDictionary<string, Func<GameRules>> RulesByGameTypeCode =
    new Dictionary<string, Func<GameRules>>(StringComparer.OrdinalIgnoreCase)
    {
        [PokerGameMetadataRegistry.FiveCardDrawCode] = FiveCardDrawRules.CreateGameRules,
        [PokerGameMetadataRegistry.KingsAndLowsCode] = KingsAndLowsRules.CreateGameRules,  // NEW
        // ... other games
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
```

### Step 5: Test the Game Rules

You can now retrieve your game rules via the API:

```bash
curl http://localhost:5000/api/v1/games/rules/KINGSANDLOWS
```

Or programmatically:

```csharp
if (PokerGameRulesRegistry.TryGet("KINGSANDLOWS", out var rules))
{
    Console.WriteLine($"Game: {rules.GameTypeName}");
    Console.WriteLine($"Players: {rules.MinPlayers}-{rules.MaxPlayers}");
    Console.WriteLine($"Phases: {rules.Phases.Count}");
    
    foreach (var phase in rules.Phases)
    {
        Console.WriteLine($"  - {phase.Name} ({phase.Category})");
    }
}
```

## Guidelines for Defining Game Rules

### Phases

- **Order matters**: List phases in the order they occur during gameplay
- **Use consistent categories**: "Setup", "Dealing", "Betting", "Drawing", "Decision", "Resolution", "Special"
- **Mark action phases**: Set `RequiresPlayerAction = true` for phases where players make decisions
- **List available actions**: Specify all actions players can take (e.g., "Check", "Bet", "Fold", "Draw")
- **Terminal phases**: Mark the final phase with `IsTerminal = true`

### Card Dealing

- **InitialCards**: Number of cards each player receives at the start
- **InitialVisibility**: "FaceDown" for hole cards, "FaceUp" for exposed cards
- **HasCommunityCards**: `true` for Hold'em/Omaha-style games
- **DealingRounds**: For stud games, specify each dealing round (count, visibility, target)

### Betting

- **HasAntes**: Whether all players post antes before the hand
- **HasBlinds**: Whether the game uses blinds (typical in Hold'em/Omaha)
- **BettingRounds**: Number of betting rounds (0 for games without betting)
- **Structure**: "Fixed Limit", "Pot Limit", "No Limit", or "No Betting"

### Drawing

- **AllowsDrawing**: `true` for draw poker variants
- **MaxDiscards**: Maximum cards a player can exchange (null for unlimited)
- **SpecialRules**: Describe any special discard rules (e.g., "4 cards if holding an Ace")
- **DrawingRounds**: Number of draw rounds (usually 1)

### Showdown

- **HandRanking**: Describe the ranking system (e.g., "Standard Poker (High)", "Razz (Low)", "with Wild Cards")
- **IsHighLow**: `true` for high-low split games
- **HasSpecialSplitRules**: `true` if the pot is split in unusual ways
- **SpecialSplitDescription**: Explain the split rules if applicable

### Special Rules

Use the `SpecialRules` dictionary for game-unique features:

```csharp
SpecialRules = new Dictionary<string, object>
{
    ["WildCards"] = "All 2s, all Jacks, and King of Diamonds",
    ["BuyCards"] = true,
    ["MinimumHandQualifier"] = "Pair of Jacks",
    ["DealerRotation"] = "Clockwise after each hand"
}
```

The UI can query these rules to enable special features.

## Common Patterns

### Standard Draw Poker
```csharp
Phases = [WaitingToStart, CollectingAntes, Dealing, FirstBetting, Draw, SecondBetting, Showdown, Complete]
CardDealing = { InitialCards = 5, Visibility = FaceDown }
Betting = { HasAntes = true, BettingRounds = 2 }
Drawing = { AllowsDrawing = true, MaxDiscards = 3 }
```

### Hold'em/Omaha Pattern
```csharp
Phases = [WaitingToStart, PostingBlinds, Dealing, Preflop, Flop, Turn, River, Showdown, Complete]
CardDealing = { InitialCards = 2 (Hold'em) or 4 (Omaha), HasCommunityCards = true }
Betting = { HasBlinds = true, BettingRounds = 4 }
Drawing = null  // No drawing in community card games
```

### Seven Card Stud Pattern
```csharp
Phases = [WaitingToStart, CollectingAntes, DealFirstStreet, ThirdStreet, FourthStreet, FifthStreet, SixthStreet, SeventhStreet, Showdown, Complete]
CardDealing = { 
    DealingRounds = [
        { Count = 2, Visibility = FaceDown, Target = Players },
        { Count = 1, Visibility = FaceUp, Target = Players },
        { Count = 1, Visibility = FaceUp, Target = Players },
        { Count = 1, Visibility = FaceUp, Target = Players },
        { Count = 1, Visibility = FaceUp, Target = Players },
        { Count = 1, Visibility = FaceDown, Target = Players }
    ]
}
Betting = { HasAntes = true, BettingRounds = 5 }
Drawing = null  // No drawing in stud
```

## Testing Your Game Rules

Create unit tests to verify your rules:

```csharp
[Fact]
public void KingsAndLows_Rules_Should_Have_Correct_Structure()
{
    // Arrange & Act
    var rules = KingsAndLowsRules.CreateGameRules();
    
    // Assert
    Assert.Equal("KINGSANDLOWS", rules.GameTypeCode);
    Assert.Equal(2, rules.MinPlayers);
    Assert.Equal(5, rules.MaxPlayers);
    Assert.Equal(7, rules.Phases.Count);
    Assert.True(rules.Betting.HasAntes);
    Assert.False(rules.Betting.HasBlinds);
    Assert.True(rules.Drawing.AllowsDrawing);
}

[Fact]
public void KingsAndLows_Rules_Should_Be_Retrievable_From_Registry()
{
    // Act
    var success = PokerGameRulesRegistry.TryGet("KINGSANDLOWS", out var rules);
    
    // Assert
    Assert.True(success);
    Assert.NotNull(rules);
    Assert.Equal("Kings and Lows", rules.GameTypeName);
}
```

## Troubleshooting

### Game rules not appearing in API

1. Check that you added the game to `PokerGameRulesRegistry`
2. Verify the game type code matches the registry key
3. Ensure `CreateGameRules()` doesn't throw exceptions

### UI not adapting to new game

1. Confirm the game rules API endpoint returns data
2. Check that phase IDs in the rules match the phase enum values
3. Verify available actions are spelled correctly

### Build errors

1. Add `using CardGames.Poker.Games.GameFlow;` to your rules file
2. Add `using System.Collections.Generic;` if creating lists/dictionaries
3. Ensure all required properties are initialized in GameRules

## Next Steps

After implementing your game rules:

1. **Test in isolation**: Use unit tests to verify the rules structure
2. **Test via API**: Call the `/api/v1/games/rules/{code}` endpoint
3. **Create a game**: Use the existing CreateGame API to instantiate your game
4. **Play through**: Join the game and verify the phases progress correctly
5. **Validate UI**: Ensure the UI adapts to your game's phases and actions

The UI will automatically:
- Display your game's phases in the status bar
- Show action buttons based on `AvailableActions`
- Adapt the card display based on `CardDealing` config
- Apply special rules when present in the metadata

## Resources

- [ARCHITECTURE.md](ARCHITECTURE.md) - Overview of the game-agnostic architecture
- [GameRules.cs](src/CardGames.Poker/Games/GameFlow/GameRules.cs) - Domain model reference
- [GameRulesDto.cs](src/CardGames.Contracts/GameRules/GameRulesDto.cs) - API contract reference
- Example implementations:
  - [FiveCardDrawRules.cs](src/CardGames.Poker/Games/FiveCardDraw/FiveCardDrawRules.cs)
  - [KingsAndLowsRules.cs](src/CardGames.Poker/Games/KingsAndLows/KingsAndLowsRules.cs)
  - [TwosJacksManWithTheAxeRules.cs](src/CardGames.Poker/Games/TwosJacksManWithTheAxe/TwosJacksManWithTheAxeRules.cs)
