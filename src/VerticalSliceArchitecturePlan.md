# Vertical Slice Architecture Plan for CardGames Web API

This document outlines the recommended approach for implementing a Vertical Slice Architecture using Wolverine to support poker gameplay in the Web API, based on analysis of the existing CLI project functionality.

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [CLI Feature Analysis](#cli-feature-analysis)
- [Feature Slices](#feature-slices)
- [API Endpoints](#api-endpoints)
- [Work Breakdown](#work-breakdown)

---

## Architecture Overview

### Technology Stack
The API project already includes:
- **Wolverine** for message handling and HTTP endpoints
- **Marten** for event sourcing and document storage (PostgreSQL)
- **FusionCache** with Redis for distributed caching
- **FluentValidation** for request validation
- **API Versioning** via URL segment, header, or query string

### Vertical Slice Pattern with Wolverine
Each feature should be organized as a self-contained slice containing:
```
Features/
└── GameName/
    ├── CreateGame/
    │   ├── CreateGameEndpoint.cs    (Wolverine HTTP endpoint)
    │   ├── CreateGameRequest.cs     (Request DTO)
    │   ├── CreateGameResponse.cs    (Response DTO)
    │   └── CreateGameValidator.cs   (FluentValidation)
    ├── JoinGame/
    ├── StartHand/
    ├── PlaceBet/
    └── ... (other operations)
```

### Domain Event Pattern
For game state management, use Marten's event sourcing:
- **GameCreated**, **PlayerJoined**, **HandStarted**, **BetPlaced**, **CardsDealt**, etc.
- Aggregate root: `PokerGameAggregate`

---

## CLI Feature Analysis

### Current CLI Capabilities

| Mode | Features | Priority for API |
|------|----------|------------------|
| **Play** | Full gameplay with betting, multiple game variants | **High** - Core functionality |
| **Deal** | Automated dealing without betting logic | Medium - Useful for demos |
| **Simulation** | Monte Carlo odds calculations | Low - Compute-heavy, consider async |

### Supported Game Variants
1. **5-Card Draw** - Simplest rules, good starting point
2. **Texas Hold 'Em** - Most popular, complex betting positions
3. **7-Card Stud** - Multiple betting streets
4. **Omaha** - 4 hole cards variant
5. **Baseball** - Wild card variant (3s, 9s wild)
6. **Kings and Lows** - Dynamic wild cards
7. **Follow the Queen** - Wild card variant

### Core Gameplay Flow (from CLI analysis)
1. Create game with configuration (ante, blinds, min bet)
2. Add players with starting chips
3. Start hand (shuffle, deal)
4. Collect forced bets (antes/blinds)
5. Deal cards
6. Betting rounds (Check, Bet, Call, Raise, Fold, All-In)
7. Game-specific phases (Draw for 5-Card Draw, community cards for Hold 'Em)
8. Showdown and pot distribution
9. Repeat or end game

---

## Feature Slices

### Slice 1: Game Management
Core CRUD operations for poker games.

### Slice 2: Player Management
Join/leave games, manage chip stacks.

### Slice 3: Hand Lifecycle
Start hands, deal cards, manage game phases.

### Slice 4: Betting Actions
Process player betting decisions.

### Slice 5: Game State Queries
Read-only operations for game state.

### Slice 6: Odds & Simulation
Hand strength analysis and odds calculations.

---

## API Endpoints

### Game Management

#### Create Game
```
POST /api/v1/games
```

**Request:**
```json
{
  "gameType": "FiveCardDraw",
  "configuration": {
    "ante": 10,
    "minBet": 20,
    "startingChips": 1000,
    "maxPlayers": 6
  }
}
```

**Response:**
```json
{
  "gameId": "uuid",
  "gameType": "FiveCardDraw",
  "status": "WaitingForPlayers",
  "configuration": { ... },
  "createdAt": "2024-01-01T00:00:00Z"
}
```

| Game Type | Additional Configuration Fields |
|-----------|--------------------------------|
| FiveCardDraw | `ante`, `minBet` |
| HoldEm | `smallBlind`, `bigBlind` |
| SevenCardStud | `ante`, `bringIn`, `smallBet`, `bigBet` |
| Omaha | `smallBlind`, `bigBlind` |

---

#### Get Game
```
GET /api/v1/games/{gameId}
```

**Response:**
```json
{
  "gameId": "uuid",
  "gameType": "FiveCardDraw",
  "status": "InProgress",
  "phase": "FirstBettingRound",
  "players": [
    {
      "playerId": "uuid",
      "name": "Alice",
      "chipStack": 980,
      "status": "Active",
      "currentBet": 10,
      "position": 0
    }
  ],
  "pot": 30,
  "currentPlayerIndex": 1,
  "dealerPosition": 0
}
```

---

#### List Games
```
GET /api/v1/games?status=WaitingForPlayers&gameType=HoldEm
```

**Response:**
```json
{
  "games": [...],
  "totalCount": 10,
  "pageSize": 20,
  "page": 1
}
```

---

#### Delete/End Game
```
DELETE /api/v1/games/{gameId}
```

---

### Player Management

#### Join Game
```
POST /api/v1/games/{gameId}/players
```

**Request:**
```json
{
  "playerName": "Alice",
  "buyIn": 1000
}
```

**Response:**
```json
{
  "playerId": "uuid",
  "name": "Alice",
  "chipStack": 1000,
  "position": 0,
  "status": "Active"
}
```

---

#### Leave Game
```
DELETE /api/v1/games/{gameId}/players/{playerId}
```

---

#### Get Player State
```
GET /api/v1/games/{gameId}/players/{playerId}
```

**Response:**
```json
{
  "playerId": "uuid",
  "name": "Alice",
  "chipStack": 950,
  "status": "Active",
  "currentBet": 20,
  "hand": {
    "cards": ["Js", "Jd", "7c", "7h", "2s"],
    "visibleToPlayer": true
  },
  "availableActions": {
    "canCheck": false,
    "canBet": false,
    "canCall": true,
    "canRaise": true,
    "canFold": true,
    "canAllIn": true,
    "callAmount": 10,
    "minRaise": 30,
    "maxBet": 950
  }
}
```

---

### Hand Lifecycle

#### Start New Hand
```
POST /api/v1/games/{gameId}/hands
```

**Response:**
```json
{
  "handId": "uuid",
  "handNumber": 1,
  "phase": "CollectingAntes",
  "dealerPosition": 0,
  "pot": 0
}
```

---

#### Get Current Hand State
```
GET /api/v1/games/{gameId}/hands/current
```

**Response (5-Card Draw example):**
```json
{
  "handId": "uuid",
  "phase": "DrawPhase",
  "pot": 60,
  "currentPlayerToAct": "uuid",
  "players": [
    {
      "playerId": "uuid",
      "name": "Alice",
      "chipStack": 980,
      "currentBet": 0,
      "status": "Active",
      "cardCount": 5
    }
  ]
}
```

**Response (Hold 'Em example):**
```json
{
  "handId": "uuid",
  "phase": "Flop",
  "pot": 60,
  "communityCards": ["8d", "8h", "4d"],
  "currentPlayerToAct": "uuid",
  "players": [...]
}
```

---

#### Deal Cards (for Deal mode)
```
POST /api/v1/games/{gameId}/hands/current/deal
```

---

### Betting Actions

#### Place Bet/Action
```
POST /api/v1/games/{gameId}/hands/current/actions
```

**Request:**
```json
{
  "playerId": "uuid",
  "actionType": "Raise",
  "amount": 40
}
```

**Action Types:** `Check`, `Bet`, `Call`, `Raise`, `Fold`, `AllIn`

**Response:**
```json
{
  "success": true,
  "action": "Alice raises to 40",
  "newPot": 100,
  "nextPlayerToAct": "uuid",
  "roundComplete": false,
  "phaseAdvanced": false
}
```

---

### Game-Specific Actions

#### Draw Cards (5-Card Draw)
```
POST /api/v1/games/{gameId}/hands/current/draw
```

**Request:**
```json
{
  "playerId": "uuid",
  "discardIndices": [0, 2, 4]
}
```

**Response:**
```json
{
  "success": true,
  "cardsDiscarded": 3,
  "newCards": ["Kh", "9c", "3d"],
  "newHand": ["Jd", "Kh", "7h", "9c", "3d"]
}
```

---

### Showdown

#### Perform Showdown
```
POST /api/v1/games/{gameId}/hands/current/showdown
```

**Response:**
```json
{
  "success": true,
  "wonByFold": false,
  "results": [
    {
      "playerId": "uuid",
      "playerName": "Alice",
      "hand": ["Js", "Jd", "7c", "7h", "2s"],
      "handType": "TwoPair",
      "handDescription": "Two Pair, Jacks and Sevens",
      "payout": 100,
      "isWinner": true
    },
    {
      "playerId": "uuid",
      "playerName": "Bob",
      "hand": ["Ac", "Kc", "Qc", "8d", "5h"],
      "handType": "HighCard",
      "handDescription": "High Card, Ace",
      "payout": 0,
      "isWinner": false
    }
  ]
}
```

---

### Odds & Simulation (Optional/Future)

#### Calculate Hand Odds
```
POST /api/v1/simulations/odds
```

**Request:**
```json
{
  "gameType": "HoldEm",
  "playerHands": [
    {
      "name": "Hero",
      "holeCards": ["Js", "Jd"]
    },
    {
      "name": "Villain",
      "holeCards": null
    }
  ],
  "communityCards": ["8d", "8h", "4d"],
  "iterations": 10000
}
```

**Response:**
```json
{
  "players": [
    {
      "name": "Hero",
      "winProbability": 0.72,
      "tieProbability": 0.02
    },
    {
      "name": "Villain",
      "winProbability": 0.26,
      "tieProbability": 0.02
    }
  ],
  "iterations": 10000,
  "calculationTimeMs": 150
}
```

---

### WebSocket Events (for real-time updates)

Consider WebSocket support for live game updates:

| Event | Payload |
|-------|---------|
| `PlayerJoined` | `{ playerId, playerName, position }` |
| `HandStarted` | `{ handId, dealerPosition }` |
| `CardsDealt` | `{ playerId, cardCount }` (hide cards from others) |
| `BetPlaced` | `{ playerId, actionType, amount, pot }` |
| `PhaseChanged` | `{ newPhase, communityCards? }` |
| `ShowdownResult` | `{ results }` |
| `PlayerLeft` | `{ playerId }` |

---

## Work Breakdown

### Phase 1: Foundation (Recommended First)
**Goal:** Establish core game infrastructure and simple game flow

| Priority | Feature | Complexity | Dependencies |
|----------|---------|------------|--------------|
| 1.1 | Game aggregate & events | Medium | None |
| 1.2 | Create Game endpoint | Low | 1.1 |
| 1.3 | Join Game endpoint | Low | 1.1 |
| 1.4 | Get Game State endpoint | Low | 1.1 |

**Suggested Game to Start:** 5-Card Draw
- Simplest rules
- No positional blinds (just ante)
- Single draw phase
- Good test bed for betting system

---

### Phase 2: Gameplay Core
**Goal:** Implement betting and hand lifecycle

| Priority | Feature | Complexity | Dependencies |
|----------|---------|------------|--------------|
| 2.1 | Start Hand endpoint | Medium | Phase 1 |
| 2.2 | Collect Antes/Blinds | Medium | 2.1 |
| 2.3 | Deal Cards | Low | 2.2 |
| 2.4 | Betting Action endpoint | High | 2.3 |
| 2.5 | Get Available Actions | Medium | 2.4 |

---

### Phase 3: Game Completion
**Goal:** Complete game loop

| Priority | Feature | Complexity | Dependencies |
|----------|---------|------------|--------------|
| 3.1 | Draw Phase (5-Card Draw) | Medium | Phase 2 |
| 3.2 | Showdown endpoint | High | 3.1 |
| 3.3 | Pot distribution | High | 3.2 |
| 3.4 | Continue/End game logic | Medium | 3.3 |

---

### Phase 4: Additional Game Types
**Goal:** Add more poker variants

| Priority | Game Type | Complexity | Notes |
|----------|-----------|------------|-------|
| 4.1 | Texas Hold 'Em | High | Blinds, positions, community cards |
| 4.2 | 7-Card Stud | High | Multiple streets, bring-in |
| 4.3 | Omaha | Medium | Similar to Hold 'Em, 4 hole cards |
| 4.4 | Wild Card Variants | Medium | Baseball, Kings and Lows |

---

### Phase 5: Enhancements
**Goal:** Polish and advanced features

| Priority | Feature | Complexity | Notes |
|----------|---------|------------|-------|
| 5.1 | WebSocket real-time updates | High | Push game state changes |
| 5.2 | Odds calculation endpoint | Medium | Async processing recommended |
| 5.3 | Game history/replay | Medium | Leverage Marten event store |
| 5.4 | Player statistics | Low | Aggregate from events |

---

## Recommended Implementation Order

### Week 1-2: Foundation (5-Card Draw)
1. Define domain events (GameCreated, PlayerJoined, HandStarted, etc.)
2. Implement PokerGameAggregate with Marten
3. Create Game, Join Game, Get Game endpoints
4. Add validation with FluentValidation

### Week 3-4: Core Gameplay
5. Start Hand, Deal Cards endpoints
6. Betting Action endpoint with full action validation
7. Draw Phase endpoint
8. Showdown and pot distribution

### Week 5-6: Testing & Polish
9. Integration tests for full game flow
10. Error handling and edge cases
11. API documentation (OpenAPI/Swagger)

### Week 7-8: Hold 'Em
12. Add Hold 'Em specific logic (blinds, positions)
13. Community card dealing (Flop, Turn, River)
14. Adapt betting rounds for positional play

### Week 9+: Additional Features
15. Other game variants as needed
16. Real-time WebSocket updates
17. Odds calculator (async)

---

## Technical Considerations

### State Management
- Use Marten's event sourcing for game state
- Each action becomes an event that modifies the aggregate
- Full audit trail and replay capability

### Concurrency
- Optimistic concurrency with Marten
- Only one player can act at a time (current player validation)
- Consider pessimistic locking for high-stakes games

### Security
- Validate that requesting player is the current player to act
- Hide other players' hole cards in responses
- Rate limiting on bet actions

### Performance
- Cache game state with FusionCache
- Invalidate cache on state changes
- Consider read models for complex queries

### Validation Examples

The existing `BettingActionType` enum from `CardGames.Poker.Betting` can be reused:

```csharp
// Proposed validator for betting actions
// Uses existing BettingActionType enum from CardGames.Poker.Betting
public class PlaceActionRequestValidator : AbstractValidator<PlaceActionRequest>
{
    public PlaceActionRequestValidator()
    {
        RuleFor(x => x.ActionType)
            .IsInEnum()
            .WithMessage("Invalid action type");
            
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => x.ActionType is BettingActionType.Bet or BettingActionType.Raise)
            .WithMessage("Bet/Raise amount must be positive");
    }
}

// Request DTO
public record PlaceActionRequest(
    Guid PlayerId,
    BettingActionType ActionType,
    int Amount = 0);
```

---

## Sample Wolverine Endpoint

The following is a proposed pattern for implementing Wolverine endpoints. This would use a new `PokerGameAggregate` that wraps the existing game classes (like `FiveCardDrawGame`) and adds event sourcing capabilities.

```csharp
// Features/Games/CreateGame/CreateGameEndpoint.cs
// This is a proposed implementation pattern

public static class CreateGameEndpoint
{
    [WolverinePost("/api/v1/games")]
    public static async Task<CreateGameResponse> Post(
        CreateGameRequest request,
        IDocumentSession session)
    {
        var gameId = Guid.NewGuid();
        
        // GameCreated event captures the initial state
        var gameCreatedEvent = new GameCreated(
            gameId,
            request.GameType,
            request.Configuration,
            DateTime.UtcNow);
        
        // Start a Marten event stream for this game
        session.Events.StartStream<PokerGame>(gameId, gameCreatedEvent);
        await session.SaveChangesAsync();
        
        return new CreateGameResponse
        {
            GameId = gameId,
            GameType = request.GameType,
            Status = GameStatus.WaitingForPlayers,
            Configuration = request.Configuration,
            CreatedAt = DateTime.UtcNow
        };
    }
}

// Proposed event
public record GameCreated(
    Guid GameId,
    string GameType,
    GameConfiguration Configuration,
    DateTime CreatedAt);

// The PokerGame aggregate would wrap existing game classes
// like FiveCardDrawGame, HoldEmGame, etc.
```

**Note:** The existing `CardGames.Poker.Games` classes (e.g., `FiveCardDrawGame`, `HoldEmGame`) contain the core game logic. The API layer would wrap these with event-sourcing capabilities, emitting events for each state change while delegating the actual game logic to the existing domain models.

---

## Conclusion

This plan provides a structured approach to implementing a Vertical Slice Architecture for the poker Web API. Starting with 5-Card Draw allows you to establish patterns for game state management, betting, and hand evaluation before tackling more complex variants like Texas Hold 'Em.

The Wolverine + Marten combination is well-suited for this domain:
- Event sourcing captures the natural event-driven nature of poker (bets, cards dealt, etc.)
- Wolverine's message handling simplifies endpoint implementation
- The vertical slice pattern keeps each feature isolated and testable

Key success factors:
1. Start simple (5-Card Draw)
2. Reuse existing domain models from `CardGames.Poker`
3. Leverage Marten events for full game history
4. Add variants incrementally
