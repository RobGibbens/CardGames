# Game-Agnostic Architecture Design

## Overview

This document describes the current hybrid architecture for the Card Games poker system. Metadata discovery registers game types and exposes rules JSON. Starting a hand, betting, showdown, and timer auto-actions are still wired explicitly per variant.

## Problem Statement

The original architecture had several issues:

1. **Tight Coupling**: The UI (`TablePlay.razor`) and API handlers contained hardcoded game-specific logic with conditionals like `if (game == FiveCardDraw) {} else if (game == TwosJacksManWithTheAxe) {}`
2. **Limited Scalability**: Adding a new game type required changes across multiple layers (domain, API, contracts, and UI)
3. **Game Flow Assumptions**: The system assumed all games follow a Five Card Draw pattern (antes, deal 5 face-down cards, draw, two betting rounds, showdown)
4. **Duplication**: Similar logic was duplicated across different game implementations

## Solution: Rule-Driven Architecture

The architecture is **partially metadata-driven**:

1. **Game rules live in the domain layer** as structured metadata. `IPokerGame.GetGameRules()` is the wire; most games return a static `*Rules.CreateGameRules()` result.
2. **The API exposes these rules** via REST (`GET /api/v1/games/rules/{gameTypeCode}`).
3. **The UI uses those rules for some behavior** (phase labels, whether a phase requires action, available-action lists) through `GameRulesService.GetGameRulesAsync`.
4. **Hand lifecycle is still explicit.** Start, antes, deal, betting, showdown, and timer auto-actions are wired through MediatR feature folders, `IGameFlowHandler`, `TableStateBuilder`, `IGameApiRouter`, and `gameTypeCode` branches in `TablePlay.razor`.

Discovery is automatic for metadata and flow handlers. Behavior is still wired explicitly. `GameRules` drive some UI, and they do not drive every table action.

### Architecture truth table

| Subsystem | Metadata-driven? | Where wired when not |
|-----------|------------------|----------------------|
| Game list / lobby images | Yes | `PokerGameMetadataRegistry` |
| `GET` game rules JSON | Yes | `PokerGameRulesRegistry` |
| Phase labels / `RequiresPlayerAction` on table | Mostly | `TableStateBuilder` + rules registry |
| Start hand / antes / deal sequence | No (variant branches) | `TablePlay.razor`, per-variant APIs |
| Betting commands | No | `Features/Games/{Variant}/.../ProcessBettingAction` |
| Showdown | No | Variant or `Generic/PerformShowdown` |
| Timer auto-action | No | `IGameFlowHandler.SendBettingActionAsync` overrides |
| Create-game stakes (ante, blinds, stud bring-in) | No | `CreateGameCommandHandler` (stud `BringIn` / `SmallBet` / `BigBet` are not set there today; see `docs/Auditing/10-fix-bring-in-never-posted.md`) |

The live start-hand matrix is in [ADDING_NEW_GAMES.md](ADDING_NEW_GAMES.md). Update that table when `TablePlay.razor` `StartGameAsync` changes.

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
`PokerGameRulesRegistry` discovers rules by scanning `IPokerGame` implementations that carry `[PokerGameMetadata]`. For each constructible type it calls `GetGameRules()` and caches the result by game-type code. There is no dictionary to edit when adding a game. The instance must have a parameterless constructor, or a public constructor the scanner can invoke with default arguments. Static `*Rules.CreateGameRules()` is the usual implementation behind `GetGameRules()`.

Public lookup API:

```csharp
public static class PokerGameRulesRegistry
{
    public static bool TryGet(string? gameTypeCode, out GameRules? rules);
    public static GameRules Get(string gameTypeCode);
    public static IEnumerable<string> GetAvailableGameTypeCodes();
}
```

`PokerGameMetadataRegistry` uses the same attribute scan for lobby metadata (name, player counts, image). `GameFlowHandlerFactory` separately scans `IGameFlowHandler` implementations in the API assembly.

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

2. **Create a rules factory** whose phase IDs match the strings stored on the hand. Live stud streets are `ThirdStreet` through `SeventhStreet` (`SevenCardStudRules.cs`). The object below is abbreviated; `MinPlayers`, `MaxPlayers`, `CardDealing`, `Betting`, and `Showdown` are also required on `GameRules`.

```csharp
public static class SevenCardStudRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "SEVENCARDSTUD",
            GameTypeName = "Seven Card Stud",
            Phases =
            [
                new() { PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "CollectingAntes", Name = "Collecting Antes", Description = "Collect antes", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "ThirdStreet", Name = "Third Street", Description = "Bring-in betting", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "FourthStreet", Name = "Fourth Street", Description = "Fourth street betting", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "FifthStreet", Name = "Fifth Street", Description = "Fifth street betting", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "SixthStreet", Name = "Sixth Street", Description = "Sixth street betting", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "SeventhStreet", Name = "Seventh Street", Description = "Seventh street betting", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Showdown", Name = "Showdown", Description = "Compare hands", Category = "Resolution", RequiresPlayerAction = false },
                new() { PhaseId = "Complete", Name = "Complete", Description = "Hand is complete", Category = "Resolution", RequiresPlayerAction = false, IsTerminal = true }
            ]
        };
    }
}
```

3. **Return those rules from the game instance** and add a flow handler. Do not edit a registry file.

```csharp
public GameRules GetGameRules() => SevenCardStudRules.CreateGameRules();
```

`PokerGameRulesRegistry` and `GameFlowHandlerFactory` pick up the type and the handler by reflection. The step-by-step checklist, including `TablePlay` and router branches, is [ADDING_NEW_GAMES.md](ADDING_NEW_GAMES.md).

Metadata alone does not start the hand or send timer actions. Bob Barker is the reference for a dedicated action: it reuses Hold'em-family betting, and it still needed a showcase-selection endpoint, Bob Barker-specific showdown DTOs, and localized table rendering.

### UI consumption (today)

`TablePlay.razor` loads rules when the table state arrives:

```csharp
_gameRules = await GameRulesService.GetGameRulesAsync(_gameTypeCode);
```

`GameRulesService` calls `GET /api/v1/games/rules/{gameTypeCode}` (`GetGameRulesEndpoint.MapGetGameRules`). The page uses that payload for phase metadata such as `CurrentPhaseRequiresAction`. Start-hand, collect-antes, deal, and showdown still switch on `_gameTypeCode` and call a specific API client. Action buttons are not rendered from `AvailableActions` alone.

## Benefits of This Architecture

1. **Extensibility**: Most new games start from domain rules and shared infrastructure instead of a full-stack rewrite
2. **Maintainability**: Game logic is centralized and declarative
3. **Consistency**: All games follow the same metadata structure
4. **Testability**: Rules can be unit tested independently
5. **Documentation**: The rules serve as machine-readable game documentation
6. **Flexibility**: Special rules can be added via the dictionary without breaking the schema

## Migration Path

Rules metadata shipped beside the existing handlers. The UI has **partially** adopted it:

1. **Rules JSON is live** for every discovered game type.
2. **Variant switches remain** in `TablePlay.razor`, flow handlers, and per-variant MediatR commands. Generic `StartHand` and dedicated `StartHand` handlers both exist; the web client often calls the dedicated route.
3. **Further metadata adoption** (one start path, rules-only action panel) is a separate code change. Do not treat this document as a promise that those switches are gone.

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

### After (partially rule-driven)
```csharp
var phase = rules.Phases.FirstOrDefault(p => p.PhaseId == currentPhase);
if (phase?.Category == "Drawing" && phase.RequiresPlayerAction)
{
    var maxCards = rules.Drawing?.MaxDiscards ?? 5;
    ShowDrawUI(maxCards);
}
```

That pattern is used for phase metadata. Start, deal, showdown, and several action panels still use `gameTypeCode` branches.

## Conclusion

Metadata removes manual registry edits and supplies phase and dealing descriptors to the API and parts of the UI. Adding a variant that plays through the existing table still requires a flow handler and, when the sequence differs, MediatR features, a router entry, and a `TablePlay` branch. See [ADDING_NEW_GAMES.md](ADDING_NEW_GAMES.md).

## Display-Only Community Cards

Some stud variants (e.g., Tollbooth) use **shared display cards** that are visible to all players but excluded from hand evaluation. These cards are stored as `CommunityCard` type in the `GameCards` table and are queryable through the standard community cards path, but the evaluation layer ignores them.

The `TableStateBuilder` projects display cards into a game-specific private DTO (e.g., `TollboothOfferPrivateDto.DisplayCards`) so the UI can render them independently from a player's evaluated hand. When a display card is taken by a player, it is reassigned to their seat and a replacement is drawn from the deck.

This pattern enables any future variant that needs visible shared information without affecting hand ranking — for example, display-only wild card indicators, shared trump cards, or community cards that influence betting but not evaluation.
