# Phase 2: Gameplay Core - Detailed Implementation Plan

This document provides a comprehensive implementation plan for Phase 2 of the Web API development, as outlined in `VerticalSliceArchitecturePlan.md`. Phase 2 implements betting and hand lifecycle using the Wolverine Vertical Slice Architecture.

## Table of Contents
- [Overview](#overview)
- [2.1 Start Hand Endpoint](#21-start-hand-endpoint)
- [2.2 Collect Antes/Blinds](#22-collect-antesblinds)
- [2.3 Deal Cards](#23-deal-cards)
- [2.4 Betting Action Endpoint](#24-betting-action-endpoint)
- [2.5 Get Available Actions](#25-get-available-actions)
- [Domain Events](#domain-events)
- [Aggregate Changes](#aggregate-changes)
- [CLI Integration Changes](#cli-integration-changes)
- [Implementation Order](#implementation-order)
- [Testing Strategy](#testing-strategy)

---

## Overview

### Goals
- Implement the hand lifecycle (start hand, collect forced bets, deal cards)
- Implement betting action processing with full validation
- Provide available actions query for players
- Integrate with the existing `FiveCardDrawGame` domain logic

### Technology Stack
- **Wolverine** for message handling and HTTP endpoints
- **Marten** for event sourcing and document storage (PostgreSQL)
- **FluentValidation** for request validation
- **Existing CardGames.Poker** domain models for game logic (BettingRound, AvailableActions, etc.)

### New File Structure
```
Features/
└── Games/
    ├── Domain/
    │   ├── PokerGameAggregate.cs          (MODIFY - add hand state)
    │   ├── Events/
    │   │   ├── GameCreated.cs             (existing)
    │   │   ├── PlayerJoined.cs            (existing)
    │   │   ├── HandStarted.cs             (NEW)
    │   │   ├── AntesCollected.cs          (NEW)
    │   │   ├── BlindsCollected.cs         (NEW)
    │   │   ├── CardsDealt.cs              (NEW)
    │   │   └── BettingActionPerformed.cs  (NEW)
    │   └── Enums/
    │       ├── GameStatus.cs              (existing)
    │       ├── GameType.cs                (existing)
    │       └── HandPhase.cs               (NEW)
    ├── StartHand/
    │   ├── StartHandEndpoint.cs           (NEW)
    │   ├── StartHandResponse.cs           (NEW)
    │   └── StartHandValidator.cs          (NEW)
    ├── PlaceAction/
    │   ├── PlaceActionEndpoint.cs         (NEW)
    │   ├── PlaceActionRequest.cs          (NEW)
    │   ├── PlaceActionResponse.cs         (NEW)
    │   └── PlaceActionValidator.cs        (NEW)
    ├── GetAvailableActions/
    │   ├── GetAvailableActionsEndpoint.cs (NEW)
    │   └── GetAvailableActionsResponse.cs (NEW)
    └── GetCurrentHand/
        ├── GetCurrentHandEndpoint.cs      (NEW)
        └── GetCurrentHandResponse.cs      (NEW)
```

---

## 2.1 Start Hand Endpoint

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games/{gameId}/hands` |
| **Description** | Starts a new hand in the game, shuffles deck, and prepares for antes |
| **Prerequisites** | Game status must be `ReadyToStart` or `InProgress`, minimum 2 players |

### Response

**File:** `Features/Games/StartHand/StartHandResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.StartHand;

/// <summary>
/// Response returned after successfully starting a new hand.
/// </summary>
public record StartHandResponse(
    Guid HandId,
    int HandNumber,
    string Phase,
    int DealerPosition,
    int Pot,
    Guid? NextPlayerToAct
);
```

**Example Response:**
```json
{
  "handId": "770e8400-e29b-41d4-a716-446655440003",
  "handNumber": 1,
  "phase": "CollectingAntes",
  "dealerPosition": 0,
  "pot": 0,
  "nextPlayerToAct": null
}
```

### Endpoint Implementation

**File:** `Features/Games/StartHand/StartHandEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.StartHand;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

public static class StartHandEndpoint
{
    [WolverinePost("/api/v1/games/{gameId}/hands")]
    public static async Task<Results<Ok<StartHandResponse>, NotFound<string>, BadRequest<string>>> Post(
        Guid gameId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        // Load the current game state
        var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
            gameId,
            token: cancellationToken);
        
        if (game == null)
        {
            return TypedResults.NotFound($"Game with ID {gameId} not found.");
        }
        
        // Validate game can start a hand
        if (!game.CanStartHand())
        {
            return TypedResults.BadRequest("Cannot start hand - game is not ready or already has active hand.");
        }
        
        var handId = Guid.NewGuid();
        var handNumber = game.HandNumber + 1;
        var dealerPosition = game.GetNextDealerPosition();
        
        // Create the event
        var handStartedEvent = new HandStarted(
            gameId,
            handId,
            handNumber,
            dealerPosition,
            DateTime.UtcNow
        );
        
        session.Events.Append(gameId, handStartedEvent);
        await session.SaveChangesAsync(cancellationToken);
        
        return TypedResults.Ok(new StartHandResponse(
            handId,
            handNumber,
            HandPhase.CollectingAntes.ToString(),
            dealerPosition,
            Pot: 0,
            NextPlayerToAct: null
        ));
    }
}
```

### Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 200 OK | Success | `StartHandResponse` |
| 400 Bad Request | Game not ready | `"Cannot start hand - game is not ready or already has active hand."` |
| 400 Bad Request | Not enough players | `"Cannot start hand - need at least 2 players."` |
| 404 Not Found | Game not found | `"Game with ID {gameId} not found."` |

---

## 2.2 Collect Antes/Blinds

Collecting antes is performed automatically when the hand starts. The system collects forced bets from all players based on the game configuration.

### Domain Event

**File:** `Features/Games/Domain/Events/AntesCollected.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when antes are collected from all players.
/// </summary>
public record AntesCollected(
    Guid GameId,
    Guid HandId,
    Dictionary<Guid, int> PlayerAntes,  // PlayerId -> Amount collected
    int TotalCollected,
    DateTime CollectedAt
);
```

### Endpoint for Manual Ante Collection (Alternative Flow)

If you prefer manual ante collection, provide a separate endpoint:

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games/{gameId}/hands/current/collect-antes` |
| **Description** | Collects antes from all active players |

**Response:**
```json
{
  "success": true,
  "antesCollected": [
    { "playerId": "...", "playerName": "Alice", "amount": 10 },
    { "playerId": "...", "playerName": "Bob", "amount": 10 }
  ],
  "totalPot": 20,
  "phase": "Dealing"
}
```

### For Blind-Based Games (Hold'em, Omaha)

**File:** `Features/Games/Domain/Events/BlindsCollected.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when blinds are collected (Hold'em/Omaha).
/// </summary>
public record BlindsCollected(
    Guid GameId,
    Guid HandId,
    Guid SmallBlindPlayerId,
    int SmallBlindAmount,
    Guid BigBlindPlayerId,
    int BigBlindAmount,
    DateTime CollectedAt
);
```

---

## 2.3 Deal Cards

### Domain Event

**File:** `Features/Games/Domain/Events/CardsDealt.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when cards are dealt to players.
/// Note: Card values are stored encrypted/hidden in the event for security.
/// </summary>
public record CardsDealt(
    Guid GameId,
    Guid HandId,
    Dictionary<Guid, int> PlayerCardCounts,  // PlayerId -> Number of cards dealt
    DateTime DealtAt
);

/// <summary>
/// Internal event for tracking actual card values (not exposed via API).
/// </summary>
public record CardsDealtInternal(
    Guid GameId,
    Guid HandId,
    Dictionary<Guid, List<string>> PlayerCards,  // PlayerId -> Card strings (e.g., "Ah", "Kd")
    DateTime DealtAt
);
```

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games/{gameId}/hands/current/deal` |
| **Description** | Deals cards to all active players |
| **Prerequisites** | Hand must be in `CollectingAntes` or `Dealing` phase |

**Response:**
```json
{
  "success": true,
  "phase": "FirstBettingRound",
  "playerCardCounts": [
    { "playerId": "...", "playerName": "Alice", "cardCount": 5 },
    { "playerId": "...", "playerName": "Bob", "cardCount": 5 }
  ],
  "currentPlayerToAct": "660e8400-e29b-41d4-a716-446655440001"
}
```

### Get Player Cards (Private Endpoint)

| Property | Value |
|----------|-------|
| **Method** | GET |
| **Path** | `/api/v1/games/{gameId}/players/{playerId}/cards` |
| **Description** | Gets a player's cards (only visible to that player) |

**Response:**
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "cards": ["Js", "Jd", "7c", "7h", "2s"],
  "cardCount": 5
}
```

---

## 2.4 Betting Action Endpoint

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games/{gameId}/hands/current/actions` |
| **Description** | Processes a betting action from the current player |

### Request

**File:** `Features/Games/PlaceAction/PlaceActionRequest.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.PlaceAction;

using CardGames.Poker.Betting;

/// <summary>
/// Request to place a betting action.
/// </summary>
public record PlaceActionRequest(
    Guid PlayerId,
    BettingActionType ActionType,
    int Amount = 0
);
```

**Example Request Bodies:**

Check:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "Check"
}
```

Bet:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "Bet",
  "amount": 40
}
```

Call:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "Call"
}
```

Raise:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "Raise",
  "amount": 80
}
```

Fold:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "Fold"
}
```

All-In:
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "actionType": "AllIn"
}
```

### Response

**File:** `Features/Games/PlaceAction/PlaceActionResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.PlaceAction;

/// <summary>
/// Response returned after processing a betting action.
/// </summary>
public record PlaceActionResponse(
    bool Success,
    string ActionDescription,
    int NewPot,
    Guid? NextPlayerToAct,
    bool RoundComplete,
    bool PhaseAdvanced,
    string CurrentPhase,
    string? ErrorMessage = null
);
```

**Example Response (Success):**
```json
{
  "success": true,
  "actionDescription": "Alice raises to 40",
  "newPot": 100,
  "nextPlayerToAct": "770e8400-e29b-41d4-a716-446655440002",
  "roundComplete": false,
  "phaseAdvanced": false,
  "currentPhase": "FirstBettingRound"
}
```

**Example Response (Round Complete):**
```json
{
  "success": true,
  "actionDescription": "Bob calls 40",
  "newPot": 140,
  "nextPlayerToAct": null,
  "roundComplete": true,
  "phaseAdvanced": true,
  "currentPhase": "DrawPhase"
}
```

### Validation

**File:** `Features/Games/PlaceAction/PlaceActionValidator.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.PlaceAction;

using CardGames.Poker.Betting;
using FluentValidation;

public class PlaceActionValidator : AbstractValidator<PlaceActionRequest>
{
    public PlaceActionValidator()
    {
        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage("Player ID is required.");
        
        RuleFor(x => x.ActionType)
            .IsInEnum()
            .WithMessage("Invalid action type.");
        
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.ActionType is BettingActionType.Bet or BettingActionType.Raise)
            .WithMessage("Amount must be greater than 0 for Bet/Raise actions.");
        
        RuleFor(x => x.Amount)
            .Equal(0)
            .When(x => x.ActionType is BettingActionType.Check or BettingActionType.Fold)
            .WithMessage("Amount must be 0 for Check/Fold actions.");
    }
}
```

### Domain Event

**File:** `Features/Games/Domain/Events/BettingActionPerformed.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

using CardGames.Poker.Betting;

/// <summary>
/// Domain event raised when a betting action is performed.
/// </summary>
public record BettingActionPerformed(
    Guid GameId,
    Guid HandId,
    Guid PlayerId,
    BettingActionType ActionType,
    int Amount,
    int NewPot,
    int PlayerChipStack,
    bool RoundComplete,
    string? NewPhase,
    DateTime PerformedAt
);
```

### Endpoint Implementation

**File:** `Features/Games/PlaceAction/PlaceActionEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.PlaceAction;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using CardGames.Poker.Betting;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

public static class PlaceActionEndpoint
{
    [WolverinePost("/api/v1/games/{gameId}/hands/current/actions")]
    public static async Task<Results<Ok<PlaceActionResponse>, NotFound<string>, BadRequest<string>>> Post(
        Guid gameId,
        PlaceActionRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        // Load the current game state
        var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
            gameId,
            token: cancellationToken);
        
        if (game == null)
        {
            return TypedResults.NotFound($"Game with ID {gameId} not found.");
        }
        
        // Validate it's this player's turn
        if (!game.IsPlayerTurn(request.PlayerId))
        {
            return TypedResults.BadRequest("It is not this player's turn to act.");
        }
        
        // Validate action is available
        var availableActions = game.GetAvailableActions();
        var validationError = ValidateAction(request, availableActions);
        if (validationError != null)
        {
            return TypedResults.BadRequest(validationError);
        }
        
        // Process the action through domain logic
        var result = game.ProcessBettingAction(request.PlayerId, request.ActionType, request.Amount);
        
        if (!result.Success)
        {
            return TypedResults.BadRequest(result.ErrorMessage);
        }
        
        // Create and append the event
        var actionEvent = new BettingActionPerformed(
            gameId,
            game.CurrentHandId,
            request.PlayerId,
            request.ActionType,
            result.ActualAmount,
            game.TotalPot,
            result.PlayerChipStack,
            result.RoundComplete,
            result.NewPhase,
            DateTime.UtcNow
        );
        
        session.Events.Append(gameId, actionEvent);
        await session.SaveChangesAsync(cancellationToken);
        
        return TypedResults.Ok(new PlaceActionResponse(
            Success: true,
            ActionDescription: result.ActionDescription,
            NewPot: game.TotalPot,
            NextPlayerToAct: game.CurrentPlayerToAct,
            RoundComplete: result.RoundComplete,
            PhaseAdvanced: result.PhaseAdvanced,
            CurrentPhase: game.CurrentPhase.ToString()
        ));
    }
    
    private static string? ValidateAction(PlaceActionRequest request, AvailableActions available)
    {
        return request.ActionType switch
        {
            BettingActionType.Check when !available.CanCheck => 
                "Cannot check - there is a bet to match.",
            BettingActionType.Bet when !available.CanBet => 
                "Cannot bet - betting is not available.",
            BettingActionType.Bet when request.Amount < available.MinBet => 
                $"Bet must be at least {available.MinBet}.",
            BettingActionType.Bet when request.Amount > available.MaxBet => 
                $"Cannot bet more than your stack ({available.MaxBet}).",
            BettingActionType.Call when !available.CanCall => 
                "Cannot call - no bet to match.",
            BettingActionType.Raise when !available.CanRaise => 
                "Cannot raise - raising is not available.",
            BettingActionType.Raise when request.Amount < available.MinRaise => 
                $"Raise must be at least {available.MinRaise}.",
            BettingActionType.Fold when !available.CanFold && available.CanCheck => 
                "Cannot fold when you can check.",
            BettingActionType.AllIn when !available.CanAllIn => 
                "Cannot go all-in - no chips remaining.",
            _ => null
        };
    }
}
```

### Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 200 OK | Success | `PlaceActionResponse` |
| 400 Bad Request | Not player's turn | `"It is not this player's turn to act."` |
| 400 Bad Request | Invalid action | `"Cannot check - there is a bet to match."` |
| 400 Bad Request | Invalid amount | `"Bet must be at least 20."` |
| 404 Not Found | Game not found | `"Game with ID {gameId} not found."` |

---

## 2.5 Get Available Actions

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | GET |
| **Path** | `/api/v1/games/{gameId}/players/{playerId}/available-actions` |
| **Description** | Gets the available betting actions for a specific player |

### Response

**File:** `Features/Games/GetAvailableActions/GetAvailableActionsResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.GetAvailableActions;

/// <summary>
/// Response containing available betting actions for a player.
/// </summary>
public record GetAvailableActionsResponse(
    Guid PlayerId,
    bool IsCurrentPlayer,
    AvailableActionsDto Actions
);

/// <summary>
/// DTO for available actions matching the domain AvailableActions class.
/// </summary>
public record AvailableActionsDto(
    bool CanCheck,
    bool CanBet,
    bool CanCall,
    bool CanRaise,
    bool CanFold,
    bool CanAllIn,
    int MinBet,
    int MaxBet,
    int CallAmount,
    int MinRaise
);
```

**Example Response (Current Player):**
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "isCurrentPlayer": true,
  "actions": {
    "canCheck": false,
    "canBet": false,
    "canCall": true,
    "canRaise": true,
    "canFold": true,
    "canAllIn": true,
    "minBet": 20,
    "maxBet": 980,
    "callAmount": 20,
    "minRaise": 40
  }
}
```

**Example Response (Not Current Player):**
```json
{
  "playerId": "770e8400-e29b-41d4-a716-446655440002",
  "isCurrentPlayer": false,
  "actions": {
    "canCheck": false,
    "canBet": false,
    "canCall": false,
    "canRaise": false,
    "canFold": false,
    "canAllIn": false,
    "minBet": 0,
    "maxBet": 0,
    "callAmount": 0,
    "minRaise": 0
  }
}
```

### Endpoint Implementation

**File:** `Features/Games/GetAvailableActions/GetAvailableActionsEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.GetAvailableActions;

using CardGames.Poker.Api.Features.Games.Domain;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

public static class GetAvailableActionsEndpoint
{
    [WolverineGet("/api/v1/games/{gameId}/players/{playerId}/available-actions")]
    public static async Task<Results<Ok<GetAvailableActionsResponse>, NotFound<string>>> Get(
        Guid gameId,
        Guid playerId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
            gameId,
            token: cancellationToken);
        
        if (game == null)
        {
            return TypedResults.NotFound($"Game with ID {gameId} not found.");
        }
        
        var player = game.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null)
        {
            return TypedResults.NotFound($"Player with ID {playerId} not found in game.");
        }
        
        var isCurrentPlayer = game.IsPlayerTurn(playerId);
        var actions = isCurrentPlayer 
            ? game.GetAvailableActions() 
            : new AvailableActions(); // Empty actions if not current player
        
        return TypedResults.Ok(new GetAvailableActionsResponse(
            playerId,
            isCurrentPlayer,
            new AvailableActionsDto(
                actions.CanCheck,
                actions.CanBet,
                actions.CanCall,
                actions.CanRaise,
                actions.CanFold,
                actions.CanAllIn,
                actions.MinBet,
                actions.MaxBet,
                actions.CallAmount,
                actions.MinRaise
            )
        ));
    }
}
```

---

## Domain Events

### Summary of New Domain Events

| Event | Description | Key Properties |
|-------|-------------|----------------|
| `HandStarted` | A new hand begins | GameId, HandId, HandNumber, DealerPosition |
| `AntesCollected` | Antes collected from players | PlayerAntes dictionary, TotalCollected |
| `BlindsCollected` | Blinds posted (Hold'em/Omaha) | SmallBlind, BigBlind player IDs and amounts |
| `CardsDealt` | Cards dealt to players | PlayerCardCounts (public), PlayerCards (internal) |
| `BettingActionPerformed` | Player takes betting action | PlayerId, ActionType, Amount, NewPot |

### HandPhase Enum

**File:** `Features/Games/Domain/Enums/HandPhase.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Phases within a single hand.
/// </summary>
public enum HandPhase
{
    /// <summary>No active hand</summary>
    None,
    
    /// <summary>Collecting forced bets (antes or blinds)</summary>
    CollectingAntes,
    
    /// <summary>Dealing initial cards</summary>
    Dealing,
    
    /// <summary>First round of betting</summary>
    FirstBettingRound,
    
    /// <summary>Draw phase (5-Card Draw specific)</summary>
    DrawPhase,
    
    /// <summary>Second round of betting</summary>
    SecondBettingRound,
    
    /// <summary>Flop betting (Hold'em/Omaha)</summary>
    FlopBetting,
    
    /// <summary>Turn betting (Hold'em/Omaha)</summary>
    TurnBetting,
    
    /// <summary>River betting (Hold'em/Omaha)</summary>
    RiverBetting,
    
    /// <summary>Final showdown</summary>
    Showdown,
    
    /// <summary>Hand complete, results distributed</summary>
    Complete
}
```

---

## Aggregate Changes

### PokerGameAggregate Modifications

The `PokerGameAggregate` needs to be extended to support hand lifecycle:

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain;

using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;

public class PokerGameAggregate
{
    // Existing properties...
    public Guid Id { get; private set; }
    public GameType GameType { get; private set; }
    public GameStatus Status { get; private set; }
    public GameConfiguration Configuration { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public List<GamePlayer> Players { get; private set; } = [];

    // NEW: Hand state properties
    public Guid? CurrentHandId { get; private set; }
    public int HandNumber { get; private set; }
    public HandPhase CurrentPhase { get; private set; }
    public int DealerPosition { get; private set; }
    public int TotalPot { get; private set; }
    public Guid? CurrentPlayerToAct { get; private set; }
    public int CurrentBet { get; private set; }
    
    // Internal game state (wraps FiveCardDrawGame logic)
    private FiveCardDrawGame? _gameInstance;
    private Dictionary<Guid, string> _playerIdToName = new();
    private Dictionary<string, Guid> _playerNameToId = new();

    // Existing Apply methods...
    
    // NEW: Apply method for HandStarted event
    public void Apply(HandStarted @event)
    {
        CurrentHandId = @event.HandId;
        HandNumber = @event.HandNumber;
        DealerPosition = @event.DealerPosition;
        CurrentPhase = HandPhase.CollectingAntes;
        Status = GameStatus.InProgress;
        TotalPot = 0;
        CurrentBet = 0;
        
        // Initialize internal game instance
        InitializeGameInstance();
    }
    
    // NEW: Apply method for AntesCollected event
    public void Apply(AntesCollected @event)
    {
        TotalPot = @event.TotalCollected;
        CurrentPhase = HandPhase.Dealing;
        
        // Update player chip stacks
        foreach (var (playerId, amount) in @event.PlayerAntes)
        {
            var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null)
            {
                player.DeductChips(amount);
                player.CurrentBet = amount;
            }
        }
    }
    
    // NEW: Apply method for CardsDealt event
    public void Apply(CardsDealt @event)
    {
        CurrentPhase = HandPhase.FirstBettingRound;
        // Reset current bets for betting round
        foreach (var player in Players)
        {
            player.CurrentBet = 0;
        }
        CurrentBet = 0;
        
        // Set first player to act
        CurrentPlayerToAct = GetFirstPlayerToAct();
    }
    
    // NEW: Apply method for BettingActionPerformed event
    public void Apply(BettingActionPerformed @event)
    {
        TotalPot = @event.NewPot;
        
        var player = Players.FirstOrDefault(p => p.PlayerId == @event.PlayerId);
        if (player != null)
        {
            player.ChipStack = @event.PlayerChipStack;
            
            if (@event.ActionType == BettingActionType.Fold)
            {
                player.HasFolded = true;
            }
            else if (@event.ActionType == BettingActionType.AllIn)
            {
                player.IsAllIn = true;
            }
        }
        
        if (@event.RoundComplete && @event.NewPhase != null)
        {
            CurrentPhase = Enum.Parse<HandPhase>(@event.NewPhase);
        }
        
        // Update current bet and next player
        UpdateCurrentBetAndNextPlayer();
    }
    
    // NEW: Domain methods
    public bool CanStartHand()
    {
        return (Status == GameStatus.ReadyToStart || Status == GameStatus.InProgress)
            && CurrentPhase == HandPhase.None
            && Players.Count(p => p.ChipStack > 0) >= 2;
    }
    
    public int GetNextDealerPosition()
    {
        return (DealerPosition + 1) % Players.Count;
    }
    
    public bool IsPlayerTurn(Guid playerId)
    {
        return CurrentPlayerToAct == playerId;
    }
    
    public AvailableActions GetAvailableActions()
    {
        if (_gameInstance == null || CurrentPlayerToAct == null)
        {
            return new AvailableActions();
        }
        return _gameInstance.GetAvailableActions();
    }
    
    public BettingResult ProcessBettingAction(Guid playerId, BettingActionType actionType, int amount)
    {
        if (_gameInstance == null)
        {
            return new BettingResult { Success = false, ErrorMessage = "No active hand" };
        }
        
        var result = _gameInstance.ProcessBettingAction(actionType, amount);
        
        return new BettingResult
        {
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            ActionDescription = result.Action?.ToString() ?? "",
            ActualAmount = result.Action?.Amount ?? 0,
            RoundComplete = result.RoundComplete,
            PhaseAdvanced = result.RoundComplete,
            NewPhase = result.RoundComplete ? GetNextPhase().ToString() : null,
            PlayerChipStack = GetPlayerChipStack(playerId)
        };
    }
    
    private void InitializeGameInstance()
    {
        var playerTuples = Players.Select(p => (p.Name, p.ChipStack)).ToList();
        _gameInstance = new FiveCardDrawGame(playerTuples, Configuration.Ante, Configuration.MinBet);
        
        _playerIdToName = Players.ToDictionary(p => p.PlayerId, p => p.Name);
        _playerNameToId = Players.ToDictionary(p => p.Name, p => p.PlayerId);
    }
    
    private Guid? GetFirstPlayerToAct()
    {
        var activePlayer = Players
            .Skip((DealerPosition + 1) % Players.Count)
            .Concat(Players.Take((DealerPosition + 1) % Players.Count))
            .FirstOrDefault(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);
        
        return activePlayer?.PlayerId;
    }
    
    private void UpdateCurrentBetAndNextPlayer()
    {
        // Logic to determine next player based on game state
        if (_gameInstance != null)
        {
            var currentPlayer = _gameInstance.GetCurrentPlayer();
            if (currentPlayer != null && _playerNameToId.TryGetValue(currentPlayer.Name, out var playerId))
            {
                CurrentPlayerToAct = playerId;
            }
            else
            {
                CurrentPlayerToAct = null;
            }
            CurrentBet = _gameInstance.CurrentBettingRound?.CurrentBet ?? 0;
        }
    }
    
    private HandPhase GetNextPhase()
    {
        return CurrentPhase switch
        {
            HandPhase.FirstBettingRound => HandPhase.DrawPhase,
            HandPhase.DrawPhase => HandPhase.SecondBettingRound,
            HandPhase.SecondBettingRound => HandPhase.Showdown,
            _ => HandPhase.Complete
        };
    }
    
    private int GetPlayerChipStack(Guid playerId)
    {
        return Players.FirstOrDefault(p => p.PlayerId == playerId)?.ChipStack ?? 0;
    }
}

/// <summary>
/// Result of processing a betting action in the aggregate.
/// </summary>
public class BettingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string ActionDescription { get; init; } = "";
    public int ActualAmount { get; init; }
    public bool RoundComplete { get; init; }
    public bool PhaseAdvanced { get; init; }
    public string? NewPhase { get; init; }
    public int PlayerChipStack { get; init; }
}
```

### GamePlayer Modifications

**File:** `Features/Games/Domain/GamePlayer.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Represents a player in the game aggregate.
/// </summary>
public class GamePlayer
{
    public Guid PlayerId { get; init; }
    public string Name { get; init; }
    public int ChipStack { get; set; }
    public int Position { get; init; }
    
    // NEW: Hand state
    public int CurrentBet { get; set; }
    public bool HasFolded { get; set; }
    public bool IsAllIn { get; set; }
    public List<string> Cards { get; set; } = [];
    
    public GamePlayer(Guid playerId, string name, int chipStack, int position)
    {
        PlayerId = playerId;
        Name = name;
        ChipStack = chipStack;
        Position = position;
    }
    
    public void DeductChips(int amount)
    {
        ChipStack -= Math.Min(amount, ChipStack);
    }
    
    public void AddChips(int amount)
    {
        ChipStack += amount;
    }
    
    public void ResetForNewHand()
    {
        CurrentBet = 0;
        HasFolded = false;
        IsAllIn = false;
        Cards.Clear();
    }
}
```

---

## CLI Integration Changes

### Overview

To integrate Phase 2 API calls into the CLI, we need to:
1. Extend the `ApiClient` with new methods for hand lifecycle
2. Extend `ApiModels` with new request/response DTOs
3. Modify `ApiFiveCardDrawPlayCommand` to use API for gameplay

### 1. Extend ApiClient

**File:** `CardGames.Poker.CLI/Api/ApiClient.cs` (additions)

```csharp
// Add to existing ApiClient class

/// <summary>
/// Starts a new hand in the game.
/// </summary>
public async Task<StartHandResponse?> StartHandAsync(Guid gameId)
{
    var response = await _httpClient.PostAsync($"/api/v1/games/{gameId}/hands", null);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<StartHandResponse>(_jsonOptions);
}

/// <summary>
/// Gets the current hand state.
/// </summary>
public async Task<GetCurrentHandResponse?> GetCurrentHandAsync(Guid gameId)
{
    return await _httpClient.GetFromJsonAsync<GetCurrentHandResponse>(
        $"/api/v1/games/{gameId}/hands/current",
        _jsonOptions);
}

/// <summary>
/// Gets a player's cards.
/// </summary>
public async Task<GetPlayerCardsResponse?> GetPlayerCardsAsync(Guid gameId, Guid playerId)
{
    return await _httpClient.GetFromJsonAsync<GetPlayerCardsResponse>(
        $"/api/v1/games/{gameId}/players/{playerId}/cards",
        _jsonOptions);
}

/// <summary>
/// Gets available actions for a player.
/// </summary>
public async Task<GetAvailableActionsResponse?> GetAvailableActionsAsync(Guid gameId, Guid playerId)
{
    return await _httpClient.GetFromJsonAsync<GetAvailableActionsResponse>(
        $"/api/v1/games/{gameId}/players/{playerId}/available-actions",
        _jsonOptions);
}

/// <summary>
/// Places a betting action.
/// </summary>
public async Task<PlaceActionResponse?> PlaceActionAsync(Guid gameId, PlaceActionRequest request)
{
    var response = await _httpClient.PostAsJsonAsync(
        $"/api/v1/games/{gameId}/hands/current/actions",
        request,
        _jsonOptions);
    
    if (!response.IsSuccessStatusCode)
    {
        var error = await response.Content.ReadAsStringAsync();
        return new PlaceActionResponse(false, "", 0, null, false, false, "", error);
    }
    
    return await response.Content.ReadFromJsonAsync<PlaceActionResponse>(_jsonOptions);
}
```

### 2. Extend ApiModels

**File:** `CardGames.Poker.CLI/Api/ApiModels.cs` (additions)

```csharp
// Add to existing ApiModels.cs file

// Betting action type enum (matches API)
public enum BettingActionType
{
    Check,
    Bet,
    Call,
    Raise,
    Fold,
    AllIn,
    Post
}

// Hand lifecycle request/response DTOs
public record StartHandResponse(
    Guid HandId,
    int HandNumber,
    string Phase,
    int DealerPosition,
    int Pot,
    Guid? NextPlayerToAct
);

public record GetCurrentHandResponse(
    Guid HandId,
    string Phase,
    int Pot,
    Guid? CurrentPlayerToAct,
    int CurrentBet,
    List<HandPlayerStateResponse> Players
);

public record HandPlayerStateResponse(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int CurrentBet,
    string Status,
    int CardCount
);

public record GetPlayerCardsResponse(
    Guid PlayerId,
    List<string> Cards,
    int CardCount
);

public record GetAvailableActionsResponse(
    Guid PlayerId,
    bool IsCurrentPlayer,
    AvailableActionsDto Actions
);

public record AvailableActionsDto(
    bool CanCheck,
    bool CanBet,
    bool CanCall,
    bool CanRaise,
    bool CanFold,
    bool CanAllIn,
    int MinBet,
    int MaxBet,
    int CallAmount,
    int MinRaise
);

public record PlaceActionRequest(
    Guid PlayerId,
    BettingActionType ActionType,
    int Amount = 0
);

public record PlaceActionResponse(
    bool Success,
    string ActionDescription,
    int NewPot,
    Guid? NextPlayerToAct,
    bool RoundComplete,
    bool PhaseAdvanced,
    string CurrentPhase,
    string? ErrorMessage = null
);
```

### 3. Modify ApiFiveCardDrawPlayCommand

**File:** `CardGames.Poker.CLI/Play/ApiFiveCardDrawPlayCommand.cs` (modified)

Replace the Phase 1 placeholder with full gameplay:

```csharp
namespace CardGames.Poker.CLI.Play;

using CardGames.Poker.CLI.Api;
using CardGames.Poker.CLI.Output;
using Spectre.Console;
using Spectre.Console.Cli;

internal class ApiFiveCardDrawPlayCommand : AsyncCommand<ApiPlaySettings>
{
    private static readonly SpectreLogger Logger = new();

    public override async Task<int> ExecuteAsync(CommandContext context, ApiPlaySettings settings)
    {
        Logger.LogApplicationStart();
        
        var apiUrl = settings.ApiUrl ?? "https://localhost:7034";
        using var apiClient = new ApiClient(apiUrl);
        
        try
        {
            // Phase 1: Create game and add players (existing code)
            var gameId = await SetupGame(apiClient, settings);
            if (gameId == null) return 1;
            
            var playerIds = await GetPlayerIds(apiClient, gameId.Value);
            
            // Phase 2: Play hands
            do
            {
                await PlayHand(apiClient, gameId.Value, playerIds);
            }
            while (AnsiConsole.Confirm("Play another hand?"));
            
            Logger.Paragraph("Game Over");
            await DisplayFinalStandings(apiClient, gameId.Value);
            
            return 0;
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error: {ex.Message}[/]");
            return 1;
        }
    }
    
    private async Task PlayHand(ApiClient apiClient, Guid gameId, List<(Guid id, string name)> playerIds)
    {
        Logger.Paragraph("New Hand");
        
        // Start hand (triggers ante collection and dealing automatically)
        var startResponse = await apiClient.StartHandAsync(gameId);
        if (startResponse == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to start hand.[/]");
            return;
        }
        
        AnsiConsole.MarkupLine($"[green]Hand #{startResponse.HandNumber} started[/]");
        AnsiConsole.MarkupLine($"[dim]Dealer: Position {startResponse.DealerPosition}[/]");
        
        // Wait for dealing to complete
        await Task.Delay(500);
        
        // Get current hand state
        var handState = await apiClient.GetCurrentHandAsync(gameId);
        AnsiConsole.MarkupLine($"[green]Pot: {handState?.Pot}[/]");
        
        // First betting round
        if (!await RunBettingRound(apiClient, gameId, playerIds, "First Betting Round"))
        {
            await ShowResults(apiClient, gameId);
            return;
        }
        
        // Draw phase would go here (Phase 3)
        AnsiConsole.MarkupLine("[yellow]Draw phase - coming in Phase 3[/]");
        
        // Second betting round
        if (!await RunBettingRound(apiClient, gameId, playerIds, "Second Betting Round"))
        {
            await ShowResults(apiClient, gameId);
            return;
        }
        
        // Showdown
        await ShowResults(apiClient, gameId);
    }
    
    private async Task<bool> RunBettingRound(
        ApiClient apiClient, 
        Guid gameId, 
        List<(Guid id, string name)> playerIds,
        string roundName)
    {
        Logger.Paragraph(roundName);
        
        while (true)
        {
            var handState = await apiClient.GetCurrentHandAsync(gameId);
            if (handState == null || handState.CurrentPlayerToAct == null)
            {
                return true; // Round complete
            }
            
            var currentPlayerId = handState.CurrentPlayerToAct.Value;
            var currentPlayer = playerIds.FirstOrDefault(p => p.id == currentPlayerId);
            
            // Display game state
            AnsiConsole.MarkupLine($"[green]Pot: {handState.Pot}[/] | [yellow]Current Bet: {handState.CurrentBet}[/]");
            DisplayHandPlayers(handState.Players, currentPlayerId);
            
            // Show current player's cards
            var cards = await apiClient.GetPlayerCardsAsync(gameId, currentPlayerId);
            if (cards != null)
            {
                AnsiConsole.MarkupLine($"[cyan]{currentPlayer.name}[/]'s hand: {string.Join(" ", cards.Cards)}");
            }
            
            // Get available actions
            var actionsResponse = await apiClient.GetAvailableActionsAsync(gameId, currentPlayerId);
            if (actionsResponse == null || !actionsResponse.IsCurrentPlayer)
            {
                continue;
            }
            
            // Prompt for action
            var action = PromptForAction(currentPlayer.name, actionsResponse.Actions);
            
            // Submit action
            var request = new PlaceActionRequest(currentPlayerId, action.ActionType, action.Amount);
            var result = await apiClient.PlaceActionAsync(gameId, request);
            
            if (result == null || !result.Success)
            {
                AnsiConsole.MarkupLine($"[red]{result?.ErrorMessage ?? "Failed to process action"}[/]");
                continue;
            }
            
            AnsiConsole.MarkupLine($"[blue]{result.ActionDescription}[/]");
            
            if (result.RoundComplete)
            {
                return result.CurrentPhase != "Showdown";
            }
            
            // Check if only one player remains
            var updatedState = await apiClient.GetCurrentHandAsync(gameId);
            var activePlayers = updatedState?.Players.Count(p => p.Status != "Folded") ?? 0;
            if (activePlayers <= 1)
            {
                return false;
            }
        }
    }
    
    private static (BettingActionType ActionType, int Amount) PromptForAction(
        string playerName, 
        AvailableActionsDto available)
    {
        var choices = new List<string>();
        
        if (available.CanCheck) choices.Add("Check");
        if (available.CanBet) choices.Add($"Bet ({available.MinBet}-{available.MaxBet})");
        if (available.CanCall) choices.Add($"Call {available.CallAmount}");
        if (available.CanRaise) choices.Add($"Raise (min {available.MinRaise})");
        if (available.CanFold) choices.Add("Fold");
        if (available.CanAllIn) choices.Add($"All-In ({available.MaxBet})");
        
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[cyan]{playerName}[/] - Your action:")
                .AddChoices(choices));
        
        if (choice == "Check") return (BettingActionType.Check, 0);
        if (choice.StartsWith("Call")) return (BettingActionType.Call, available.CallAmount);
        if (choice == "Fold") return (BettingActionType.Fold, 0);
        if (choice.StartsWith("All-In")) return (BettingActionType.AllIn, available.MaxBet);
        if (choice.StartsWith("Bet"))
        {
            var amount = AnsiConsole.Ask<int>($"Bet amount ({available.MinBet}-{available.MaxBet}): ");
            return (BettingActionType.Bet, amount);
        }
        if (choice.StartsWith("Raise"))
        {
            var amount = AnsiConsole.Ask<int>($"Raise to (min {available.MinRaise}): ");
            return (BettingActionType.Raise, amount);
        }
        
        return (BettingActionType.Fold, 0);
    }
    
    private static void DisplayHandPlayers(List<HandPlayerStateResponse> players, Guid currentPlayerId)
    {
        var playersInfo = players.Select(p =>
        {
            var marker = p.PlayerId == currentPlayerId ? "►" : " ";
            var status = p.Status == "Folded" ? "(folded)" : 
                        p.Status == "AllIn" ? "(all-in)" : "";
            var bet = p.CurrentBet > 0 ? $"bet: {p.CurrentBet}" : "";
            return $"{marker} {p.Name}: {p.ChipStack} chips {bet} {status}";
        });
        
        AnsiConsole.MarkupLine($"[dim]{string.Join(" | ", playersInfo)}[/]");
    }
    
    // ... existing helper methods (SetupGame, GetPlayerIds, DisplayFinalStandings, etc.)
}
```

---

## Implementation Order

### Step-by-Step Implementation

```
Phase 2 Implementation Order
============================

1. Domain Layer Extensions
   ├── Create Enums/HandPhase.cs
   ├── Create Events/HandStarted.cs
   ├── Create Events/AntesCollected.cs
   ├── Create Events/CardsDealt.cs
   ├── Create Events/BettingActionPerformed.cs
   ├── Modify GamePlayer.cs (add hand state properties)
   └── Modify PokerGameAggregate.cs (add hand lifecycle methods)

2. Start Hand Feature (2.1)
   ├── Create StartHand/StartHandResponse.cs
   ├── Create StartHand/StartHandEndpoint.cs
   └── Update MapFeatureEndpoints.cs

3. Deal Cards Feature (2.3)
   ├── Create DealCards/DealCardsEndpoint.cs
   ├── Create DealCards/DealCardsResponse.cs
   ├── Create GetPlayerCards/GetPlayerCardsEndpoint.cs
   └── Create GetPlayerCards/GetPlayerCardsResponse.cs

4. Betting Action Feature (2.4)
   ├── Create PlaceAction/PlaceActionRequest.cs
   ├── Create PlaceAction/PlaceActionResponse.cs
   ├── Create PlaceAction/PlaceActionValidator.cs
   └── Create PlaceAction/PlaceActionEndpoint.cs

5. Get Available Actions Feature (2.5)
   ├── Create GetAvailableActions/GetAvailableActionsResponse.cs
   └── Create GetAvailableActions/GetAvailableActionsEndpoint.cs

6. Get Current Hand Feature
   ├── Create GetCurrentHand/GetCurrentHandResponse.cs
   └── Create GetCurrentHand/GetCurrentHandEndpoint.cs

7. CLI Integration
   ├── Extend Api/ApiModels.cs (add new DTOs)
   ├── Extend Api/ApiClient.cs (add new methods)
   └── Modify Play/ApiFiveCardDrawPlayCommand.cs (implement gameplay loop)
```

---

## Testing Strategy

### Unit Tests

**Test Project:** `Tests/CardGames.Poker.Api.Tests/`

#### Aggregate Tests
```csharp
public class PokerGameAggregatePhase2Tests
{
    [Fact]
    public void Apply_HandStarted_SetsHandState()
    {
        // Arrange: Create game with 2 players
        // Act: Apply HandStarted event
        // Assert: CurrentHandId set, Phase is CollectingAntes
    }
    
    [Fact]
    public void CanStartHand_ReadyGame_ReturnsTrue() { }
    
    [Fact]
    public void CanStartHand_ActiveHand_ReturnsFalse() { }
    
    [Fact]
    public void IsPlayerTurn_CurrentPlayer_ReturnsTrue() { }
    
    [Fact]
    public void IsPlayerTurn_OtherPlayer_ReturnsFalse() { }
    
    [Fact]
    public void ProcessBettingAction_ValidCheck_Succeeds() { }
    
    [Fact]
    public void ProcessBettingAction_InvalidAction_ReturnsError() { }
}
```

#### Validator Tests
```csharp
public class PlaceActionValidatorTests
{
    [Fact]
    public void Validate_BetWithZeroAmount_ReturnsError() { }
    
    [Fact]
    public void Validate_CheckWithAmount_ReturnsError() { }
    
    [Fact]
    public void Validate_ValidBet_Succeeds() { }
}
```

### Integration Tests

```csharp
public class HandLifecycleEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task StartHand_ReadyGame_StartsHand() { }
    
    [Fact]
    public async Task StartHand_NotReady_ReturnsBadRequest() { }
    
    [Fact]
    public async Task PlaceAction_ValidCheck_ProcessesAction() { }
    
    [Fact]
    public async Task PlaceAction_NotPlayersTurn_ReturnsBadRequest() { }
    
    [Fact]
    public async Task GetAvailableActions_CurrentPlayer_ReturnsActions() { }
    
    [Fact]
    public async Task FullBettingRound_AllPlayersCheck_CompletesRound() { }
}
```

---

## API Endpoint Summary

| Endpoint | Method | Path | Description |
|----------|--------|------|-------------|
| Start Hand | POST | `/api/v1/games/{gameId}/hands` | Start a new hand |
| Get Current Hand | GET | `/api/v1/games/{gameId}/hands/current` | Get current hand state |
| Deal Cards | POST | `/api/v1/games/{gameId}/hands/current/deal` | Deal cards to players |
| Get Player Cards | GET | `/api/v1/games/{gameId}/players/{playerId}/cards` | Get player's cards |
| Place Action | POST | `/api/v1/games/{gameId}/hands/current/actions` | Submit betting action |
| Get Available Actions | GET | `/api/v1/games/{gameId}/players/{playerId}/available-actions` | Get available actions |

---

## Conclusion

This Phase 2 implementation plan provides:

1. **Complete hand lifecycle** - Start hand, collect antes, deal cards
2. **Full betting system** - Process all action types with validation
3. **Available actions query** - Players can see what actions are available
4. **Domain event sourcing** - All actions recorded as events
5. **CLI integration** - Full gameplay through API calls

After Phase 2 is complete, the system will support:
- Starting and managing hands
- Full betting rounds with all standard actions
- Player card visibility (private to each player)
- Complete betting round lifecycle

Phase 3 will build on this foundation to add:
- Draw phase (discard and draw cards)
- Showdown endpoint
- Pot distribution
- Winner determination
