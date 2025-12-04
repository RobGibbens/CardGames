# Phase 1: Foundation - Detailed Implementation Plan

This document provides a comprehensive implementation plan for Phase 1 of the Web API development, as outlined in `VerticalSliceArchitecturePlan.md`. Phase 1 establishes the core game infrastructure for 5-Card Draw poker using the Wolverine Vertical Slice Architecture.

## Table of Contents
- [Overview](#overview)
- [1.1 Game Aggregate & Events](#11-game-aggregate--events)
- [1.2 Create Game Endpoint](#12-create-game-endpoint)
- [1.3 Join Game Endpoint](#13-join-game-endpoint)
- [1.4 Get Game State Endpoint](#14-get-game-state-endpoint)
- [CLI Integration Changes](#cli-integration-changes)
- [Implementation Order](#implementation-order)
- [Testing Strategy](#testing-strategy)

---

## Overview

### Goals
- Establish the event-sourcing foundation using Marten
- Implement the core `PokerGameAggregate` that wraps existing game logic
- Create three essential API endpoints: Create Game, Join Game, Get Game State
- Define the domain events for game state management

### Technology Stack
- **Wolverine** for message handling and HTTP endpoints
- **Marten** for event sourcing and document storage (PostgreSQL)
- **FluentValidation** for request validation
- **Existing CardGames.Poker** domain models for game logic

### File Structure
```
Features/
└── Games/
    ├── Domain/
    │   ├── PokerGameAggregate.cs
    │   ├── Events/
    │   │   ├── GameCreated.cs
    │   │   └── PlayerJoined.cs
    │   └── Enums/
    │       ├── GameStatus.cs
    │       └── GameType.cs
    ├── CreateGame/
    │   ├── CreateGameEndpoint.cs
    │   ├── CreateGameRequest.cs
    │   ├── CreateGameResponse.cs
    │   └── CreateGameValidator.cs
    ├── JoinGame/
    │   ├── JoinGameEndpoint.cs
    │   ├── JoinGameRequest.cs
    │   ├── JoinGameResponse.cs
    │   └── JoinGameValidator.cs
    └── GetGameState/
        ├── GetGameStateEndpoint.cs
        └── GetGameStateResponse.cs
```

---

## 1.1 Game Aggregate & Events

### Domain Events

#### GameCreated Event
**File:** `Features/Games/Domain/Events/GameCreated.cs`

This event captures the initial creation of a poker game.

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a new poker game is created.
/// </summary>
public record GameCreated(
    Guid GameId,
    GameType GameType,
    GameConfiguration Configuration,
    DateTime CreatedAt
);
```

**Purpose:** Records the initial game creation with its configuration.

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `GameId` | `Guid` | Unique identifier for the game |
| `GameType` | `GameType` | Type of poker game (e.g., FiveCardDraw) |
| `Configuration` | `GameConfiguration` | Game settings (ante, minBet, maxPlayers, startingChips) |
| `CreatedAt` | `DateTime` | UTC timestamp of creation |

---

#### PlayerJoined Event
**File:** `Features/Games/Domain/Events/PlayerJoined.cs`

This event captures when a player joins a game.

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a player joins a poker game.
/// </summary>
public record PlayerJoined(
    Guid GameId,
    Guid PlayerId,
    string PlayerName,
    int BuyIn,
    int Position,
    DateTime JoinedAt
);
```

**Purpose:** Records a player joining the game with their initial buy-in.

**Properties:**
| Property | Type | Description |
|----------|------|-------------|
| `GameId` | `Guid` | Game the player is joining |
| `PlayerId` | `Guid` | Unique identifier for the player in this game |
| `PlayerName` | `string` | Display name of the player |
| `BuyIn` | `int` | Initial chip amount (may be validated against configuration) |
| `Position` | `int` | Seat position at the table (0-indexed) |
| `JoinedAt` | `DateTime` | UTC timestamp when player joined |

---

### Supporting Types

#### GameType Enum
**File:** `Features/Games/Domain/Enums/GameType.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Supported poker game variants.
/// </summary>
public enum GameType
{
    FiveCardDraw,
    TexasHoldEm,
    SevenCardStud,
    Omaha,
    Baseball,
    KingsAndLows,
    FollowTheQueen
}
```

---

#### GameStatus Enum
**File:** `Features/Games/Domain/Enums/GameStatus.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Current status of a poker game.
/// </summary>
public enum GameStatus
{
    /// <summary>Game created, waiting for players to join</summary>
    WaitingForPlayers,
    
    /// <summary>Enough players have joined, ready to start a hand</summary>
    ReadyToStart,
    
    /// <summary>A hand is currently in progress</summary>
    InProgress,
    
    /// <summary>Game has ended</summary>
    Completed,
    
    /// <summary>Game was cancelled</summary>
    Cancelled
}
```

---

#### GameConfiguration Record
**File:** `Features/Games/Domain/GameConfiguration.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Configuration settings for a poker game.
/// </summary>
public record GameConfiguration(
    int Ante,
    int MinBet,
    int StartingChips,
    int MaxPlayers
)
{
    /// <summary>Default configuration for 5-Card Draw</summary>
    public static GameConfiguration DefaultFiveCardDraw => new(
        Ante: 10,
        MinBet: 20,
        StartingChips: 1000,
        MaxPlayers: 6
    );
}
```

---

### PokerGameAggregate
**File:** `Features/Games/Domain/PokerGameAggregate.cs`

The aggregate root that manages game state through event sourcing. This class wraps the existing `FiveCardDrawGame` logic while providing event-driven state management.

```csharp
namespace CardGames.Poker.Api.Features.Games.Domain;

using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Aggregate root for a poker game, managing state through event sourcing.
/// Wraps existing game domain models (e.g., FiveCardDrawGame) while providing
/// event-driven state management compatible with Marten.
/// </summary>
public class PokerGameAggregate
{
    // Identity
    public Guid Id { get; private set; }
    
    // State
    public GameType GameType { get; private set; }
    public GameStatus Status { get; private set; }
    public GameConfiguration Configuration { get; private set; }
    public DateTime CreatedAt { get; private set; }
    
    // Players
    public List<GamePlayer> Players { get; private set; } = [];
    
    // Marten requires a default constructor
    public PokerGameAggregate() { }
    
    /// <summary>
    /// Apply method for GameCreated event (Marten convention).
    /// </summary>
    public void Apply(GameCreated @event)
    {
        Id = @event.GameId;
        GameType = @event.GameType;
        Configuration = @event.Configuration;
        Status = GameStatus.WaitingForPlayers;
        CreatedAt = @event.CreatedAt;
    }
    
    /// <summary>
    /// Apply method for PlayerJoined event (Marten convention).
    /// </summary>
    public void Apply(PlayerJoined @event)
    {
        Players.Add(new GamePlayer(
            @event.PlayerId,
            @event.PlayerName,
            @event.BuyIn,
            @event.Position
        ));
        
        // Update status if we have minimum players
        if (Players.Count >= 2)
        {
            Status = GameStatus.ReadyToStart;
        }
    }
    
    /// <summary>
    /// Check if a player can join the game.
    /// </summary>
    public bool CanPlayerJoin()
    {
        return Status == GameStatus.WaitingForPlayers || Status == GameStatus.ReadyToStart;
    }
    
    /// <summary>
    /// Check if the game is at maximum capacity.
    /// </summary>
    public bool IsFull()
    {
        return Players.Count >= Configuration.MaxPlayers;
    }
    
    /// <summary>
    /// Get the next available seat position.
    /// </summary>
    public int GetNextPosition()
    {
        return Players.Count;
    }
}

/// <summary>
/// Represents a player in the game aggregate.
/// </summary>
public record GamePlayer(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int Position
);
```

**Marten Event Sourcing Integration:**

The `Apply` methods follow Marten's convention for event sourcing. When events are appended to a stream, Marten automatically calls the corresponding `Apply` method to update aggregate state.

**Configuration in `Program.cs`:**
```csharp
builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten");
    opts.Connection(connectionString!);
    opts.DatabaseSchemaName = "poker_games";
    
    // Register the aggregate for event sourcing
    opts.Projections.Snapshot<PokerGameAggregate>(SnapshotLifecycle.Inline);
})
.IntegrateWithWolverine();
```

---

## 1.2 Create Game Endpoint

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games` |
| **Description** | Creates a new poker game with the specified configuration |
| **Authentication** | Optional (can be anonymous for initial implementation) |

### Request

**File:** `Features/Games/CreateGame/CreateGameRequest.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.CreateGame;

using CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Request to create a new poker game.
/// </summary>
public record CreateGameRequest(
    GameType GameType,
    CreateGameConfigurationRequest? Configuration = null
);

/// <summary>
/// Optional configuration overrides for game creation.
/// </summary>
public record CreateGameConfigurationRequest(
    int? Ante = null,
    int? MinBet = null,
    int? StartingChips = null,
    int? MaxPlayers = null
);
```

**Example Request Body:**
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

**Minimal Request (uses defaults):**
```json
{
  "gameType": "FiveCardDraw"
}
```

---

### Response

**File:** `Features/Games/CreateGame/CreateGameResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.CreateGame;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Response returned after successfully creating a game.
/// </summary>
public record CreateGameResponse(
    Guid GameId,
    GameType GameType,
    GameStatus Status,
    GameConfiguration Configuration,
    DateTime CreatedAt
);
```

**Example Response:**
```json
{
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "gameType": "FiveCardDraw",
  "status": "WaitingForPlayers",
  "configuration": {
    "ante": 10,
    "minBet": 20,
    "startingChips": 1000,
    "maxPlayers": 6
  },
  "createdAt": "2024-01-15T10:30:00Z"
}
```

---

### Validation

**File:** `Features/Games/CreateGame/CreateGameValidator.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.CreateGame;

using FluentValidation;

/// <summary>
/// Validates create game requests.
/// </summary>
public class CreateGameValidator : AbstractValidator<CreateGameRequest>
{
    public CreateGameValidator()
    {
        RuleFor(x => x.GameType)
            .IsInEnum()
            .WithMessage("Invalid game type specified.");
        
        When(x => x.Configuration != null, () =>
        {
            RuleFor(x => x.Configuration!.Ante)
                .GreaterThanOrEqualTo(0)
                .When(x => x.Configuration!.Ante.HasValue)
                .WithMessage("Ante must be non-negative.");
            
            RuleFor(x => x.Configuration!.MinBet)
                .GreaterThan(0)
                .When(x => x.Configuration!.MinBet.HasValue)
                .WithMessage("Minimum bet must be greater than 0.");
            
            RuleFor(x => x.Configuration!.StartingChips)
                .GreaterThan(0)
                .When(x => x.Configuration!.StartingChips.HasValue)
                .WithMessage("Starting chips must be greater than 0.");
            
            RuleFor(x => x.Configuration!.MaxPlayers)
                .InclusiveBetween(2, 6)
                .When(x => x.Configuration!.MaxPlayers.HasValue)
                .WithMessage("Max players must be between 2 and 6 for 5-Card Draw.");
        });
    }
}
```

---

### Endpoint Implementation

**File:** `Features/Games/CreateGame/CreateGameEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.CreateGame;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Wolverine.Http;

/// <summary>
/// Wolverine HTTP endpoint for creating a new poker game.
/// </summary>
public static class CreateGameEndpoint
{
    /// <summary>
    /// Creates a new poker game and starts an event stream.
    /// </summary>
    [WolverinePost("/api/v1/games")]
    public static async Task<CreateGameResponse> Post(
        CreateGameRequest request,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var gameId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        
        // Build configuration from request or use defaults
        var defaultConfig = GameConfiguration.DefaultFiveCardDraw;
        var configuration = new GameConfiguration(
            Ante: request.Configuration?.Ante ?? defaultConfig.Ante,
            MinBet: request.Configuration?.MinBet ?? defaultConfig.MinBet,
            StartingChips: request.Configuration?.StartingChips ?? defaultConfig.StartingChips,
            MaxPlayers: request.Configuration?.MaxPlayers ?? defaultConfig.MaxPlayers
        );
        
        // Create the domain event
        var gameCreatedEvent = new GameCreated(
            gameId,
            request.GameType,
            configuration,
            createdAt
        );
        
        // Start the event stream for this game
        session.Events.StartStream<PokerGameAggregate>(gameId, gameCreatedEvent);
        await session.SaveChangesAsync(cancellationToken);
        
        return new CreateGameResponse(
            gameId,
            request.GameType,
            GameStatus.WaitingForPlayers,
            configuration,
            createdAt
        );
    }
}
```

---

### Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 201 Created | Success | `CreateGameResponse` |
| 400 Bad Request | Validation fails | `{ "errors": { "gameType": ["Invalid game type specified."] } }` |
| 500 Internal Server Error | Database error | `{ "title": "An error occurred", "status": 500 }` |

---

## 1.3 Join Game Endpoint

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | POST |
| **Path** | `/api/v1/games/{gameId}/players` |
| **Description** | Adds a player to an existing game |
| **Authentication** | Optional (can be anonymous for initial implementation) |

### Request

**File:** `Features/Games/JoinGame/JoinGameRequest.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Request to join an existing poker game.
/// </summary>
public record JoinGameRequest(
    string PlayerName,
    int? BuyIn = null
);
```

**Example Request Body:**
```json
{
  "playerName": "Alice",
  "buyIn": 1000
}
```

**Minimal Request (uses game's default starting chips):**
```json
{
  "playerName": "Alice"
}
```

---

### Response

**File:** `Features/Games/JoinGame/JoinGameResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.JoinGame;

using CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Response returned after successfully joining a game.
/// </summary>
public record JoinGameResponse(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int Position,
    PlayerStatus Status
);

/// <summary>
/// Status of a player in the game.
/// </summary>
public enum PlayerStatus
{
    Active,
    Folded,
    AllIn,
    SittingOut
}
```

**Example Response:**
```json
{
  "playerId": "660e8400-e29b-41d4-a716-446655440001",
  "name": "Alice",
  "chipStack": 1000,
  "position": 0,
  "status": "Active"
}
```

---

### Validation

**File:** `Features/Games/JoinGame/JoinGameValidator.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.JoinGame;

using FluentValidation;

/// <summary>
/// Validates join game requests.
/// </summary>
public class JoinGameValidator : AbstractValidator<JoinGameRequest>
{
    public JoinGameValidator()
    {
        RuleFor(x => x.PlayerName)
            .NotEmpty()
            .WithMessage("Player name is required.")
            .MaximumLength(50)
            .WithMessage("Player name cannot exceed 50 characters.");
        
        RuleFor(x => x.BuyIn)
            .GreaterThan(0)
            .When(x => x.BuyIn.HasValue)
            .WithMessage("Buy-in must be greater than 0.");
    }
}
```

---

### Endpoint Implementation

**File:** `Features/Games/JoinGame/JoinGameEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.JoinGame;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

/// <summary>
/// Wolverine HTTP endpoint for joining an existing poker game.
/// </summary>
public static class JoinGameEndpoint
{
    /// <summary>
    /// Adds a player to an existing game.
    /// </summary>
    [WolverinePost("/api/v1/games/{gameId}/players")]
    public static async Task<Results<Ok<JoinGameResponse>, NotFound<string>, BadRequest<string>>> Post(
        Guid gameId,
        JoinGameRequest request,
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
        
        // Validate game state
        if (!game.CanPlayerJoin())
        {
            return TypedResults.BadRequest("Cannot join game - game is not accepting new players.");
        }
        
        if (game.IsFull())
        {
            return TypedResults.BadRequest($"Cannot join game - game is full ({game.Configuration.MaxPlayers} players max).");
        }
        
        // Check for duplicate player names
        if (game.Players.Any(p => p.Name.Equals(request.PlayerName, StringComparison.OrdinalIgnoreCase)))
        {
            return TypedResults.BadRequest($"A player named '{request.PlayerName}' is already in the game.");
        }
        
        var playerId = Guid.NewGuid();
        var buyIn = request.BuyIn ?? game.Configuration.StartingChips;
        var position = game.GetNextPosition();
        
        // Create and append the event
        var playerJoinedEvent = new PlayerJoined(
            gameId,
            playerId,
            request.PlayerName,
            buyIn,
            position,
            DateTime.UtcNow
        );
        
        session.Events.Append(gameId, playerJoinedEvent);
        await session.SaveChangesAsync(cancellationToken);
        
        return TypedResults.Ok(new JoinGameResponse(
            playerId,
            request.PlayerName,
            buyIn,
            position,
            PlayerStatus.Active
        ));
    }
}
```

---

### Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 200 OK | Success | `JoinGameResponse` |
| 400 Bad Request | Validation fails | `{ "errors": { "playerName": ["Player name is required."] } }` |
| 400 Bad Request | Game full | `"Cannot join game - game is full (6 players max)."` |
| 400 Bad Request | Invalid game state | `"Cannot join game - game is not accepting new players."` |
| 400 Bad Request | Duplicate name | `"A player named 'Alice' is already in the game."` |
| 404 Not Found | Game not found | `"Game with ID {gameId} not found."` |
| 500 Internal Server Error | Database error | `{ "title": "An error occurred", "status": 500 }` |

---

## 1.4 Get Game State Endpoint

### Endpoint Specification

| Property | Value |
|----------|-------|
| **Method** | GET |
| **Path** | `/api/v1/games/{gameId}` |
| **Description** | Retrieves the current state of a poker game |
| **Authentication** | Optional |

### Response

**File:** `Features/Games/GetGameState/GetGameStateResponse.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.GetGameState;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Response containing the current state of a poker game.
/// </summary>
public record GetGameStateResponse(
    Guid GameId,
    GameType GameType,
    GameStatus Status,
    GameConfiguration Configuration,
    List<PlayerStateResponse> Players,
    int DealerPosition,
    DateTime CreatedAt
);

/// <summary>
/// State of a player in the game (for public viewing).
/// </summary>
public record PlayerStateResponse(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int Position,
    PlayerStatus Status
);
```

**Example Response:**
```json
{
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "gameType": "FiveCardDraw",
  "status": "ReadyToStart",
  "configuration": {
    "ante": 10,
    "minBet": 20,
    "startingChips": 1000,
    "maxPlayers": 6
  },
  "players": [
    {
      "playerId": "660e8400-e29b-41d4-a716-446655440001",
      "name": "Alice",
      "chipStack": 1000,
      "position": 0,
      "status": "Active"
    },
    {
      "playerId": "770e8400-e29b-41d4-a716-446655440002",
      "name": "Bob",
      "chipStack": 1000,
      "position": 1,
      "status": "Active"
    }
  ],
  "dealerPosition": 0,
  "createdAt": "2024-01-15T10:30:00Z"
}
```

---

### Endpoint Implementation

**File:** `Features/Games/GetGameState/GetGameStateEndpoint.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.GetGameState;

using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.JoinGame;
using Marten;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;

/// <summary>
/// Wolverine HTTP endpoint for retrieving game state.
/// </summary>
public static class GetGameStateEndpoint
{
    /// <summary>
    /// Gets the current state of a poker game.
    /// </summary>
    [WolverineGet("/api/v1/games/{gameId}")]
    public static async Task<Results<Ok<GetGameStateResponse>, NotFound<string>>> Get(
        Guid gameId,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        // Load the current game state from events
        var game = await session.Events.AggregateStreamAsync<PokerGameAggregate>(
            gameId,
            token: cancellationToken);
        
        if (game == null)
        {
            return TypedResults.NotFound($"Game with ID {gameId} not found.");
        }
        
        // Map to response
        var players = game.Players
            .Select(p => new PlayerStateResponse(
                p.PlayerId,
                p.Name,
                p.ChipStack,
                p.Position,
                PlayerStatus.Active // In Phase 1, all players are active until game starts
            ))
            .ToList();
        
        return TypedResults.Ok(new GetGameStateResponse(
            game.Id,
            game.GameType,
            game.Status,
            game.Configuration,
            players,
            DealerPosition: 0, // Dealer position is 0 until game starts
            game.CreatedAt
        ));
    }
}
```

---

### Error Responses

| HTTP Status | Condition | Response Body |
|-------------|-----------|---------------|
| 200 OK | Success | `GetGameStateResponse` |
| 404 Not Found | Game not found | `"Game with ID {gameId} not found."` |
| 500 Internal Server Error | Database error | `{ "title": "An error occurred", "status": 500 }` |

---

## CLI Integration Changes

### Overview

To integrate the API with the CLI for Phase 1, we need to:
1. Add an HTTP client service to the CLI project
2. Create an API-backed game mode that calls the endpoints
3. Allow users to choose between local and API-backed gameplay

### Required Changes

#### 1. Add HttpClient Configuration

**File:** `CardGames.Poker.CLI/Api/ApiClient.cs`

```csharp
namespace CardGames.Poker.CLI.Api;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// HTTP client for communicating with the CardGames API.
/// </summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ApiClient(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
    
    /// <summary>
    /// Creates a new poker game.
    /// </summary>
    public async Task<CreateGameResponse?> CreateGameAsync(CreateGameRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/games", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateGameResponse>(_jsonOptions);
    }
    
    /// <summary>
    /// Joins an existing game.
    /// </summary>
    public async Task<JoinGameResponse?> JoinGameAsync(Guid gameId, JoinGameRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/games/{gameId}/players", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JoinGameResponse>(_jsonOptions);
    }
    
    /// <summary>
    /// Gets the current game state.
    /// </summary>
    public async Task<GetGameStateResponse?> GetGameStateAsync(Guid gameId)
    {
        return await _httpClient.GetFromJsonAsync<GetGameStateResponse>(
            $"/api/v1/games/{gameId}",
            _jsonOptions);
    }
    
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
```

---

#### 2. Add API Data Transfer Objects

**File:** `CardGames.Poker.CLI/Api/ApiModels.cs`

```csharp
namespace CardGames.Poker.CLI.Api;

// Request DTOs
public record CreateGameRequest(
    string GameType,
    CreateGameConfigurationRequest? Configuration = null
);

public record CreateGameConfigurationRequest(
    int? Ante = null,
    int? MinBet = null,
    int? StartingChips = null,
    int? MaxPlayers = null
);

public record JoinGameRequest(
    string PlayerName,
    int? BuyIn = null
);

// Response DTOs
public record CreateGameResponse(
    Guid GameId,
    string GameType,
    string Status,
    GameConfigurationResponse Configuration,
    DateTime CreatedAt
);

public record GameConfigurationResponse(
    int Ante,
    int MinBet,
    int StartingChips,
    int MaxPlayers
);

public record JoinGameResponse(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int Position,
    string Status
);

public record GetGameStateResponse(
    Guid GameId,
    string GameType,
    string Status,
    GameConfigurationResponse Configuration,
    List<PlayerStateResponse> Players,
    int DealerPosition,
    DateTime CreatedAt
);

public record PlayerStateResponse(
    Guid PlayerId,
    string Name,
    int ChipStack,
    int Position,
    string Status
);
```

---

#### 3. Create API-Backed Play Command

**File:** `CardGames.Poker.CLI/Play/ApiFiveCardDrawPlayCommand.cs`

```csharp
namespace CardGames.Poker.CLI.Play;

using CardGames.Poker.CLI.Api;
using CardGames.Poker.CLI.Output;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// CLI command that plays 5-Card Draw using the Web API backend.
/// </summary>
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
            // Step 1: Create the game
            var createRequest = new CreateGameRequest(
                GameType: "FiveCardDraw",
                Configuration: new CreateGameConfigurationRequest(
                    Ante: settings.Ante == default ? 10 : settings.Ante,
                    MinBet: settings.MinBet == default ? 20 : settings.MinBet,
                    StartingChips: settings.StartingChips == default ? 1000 : settings.StartingChips,
                    MaxPlayers: 6
                )
            );
            
            AnsiConsole.Status()
                .Start("Creating game...", _ =>
                {
                    // Synchronous wrapper for async call
                });
            
            var createResponse = await apiClient.CreateGameAsync(createRequest);
            if (createResponse == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to create game.[/]");
                return 1;
            }
            
            AnsiConsole.MarkupLine($"[green]Game created![/] ID: {createResponse.GameId}");
            AnsiConsole.MarkupLine($"[dim]Ante: {createResponse.Configuration.Ante} | Min Bet: {createResponse.Configuration.MinBet}[/]");
            
            // Step 2: Add players
            var numberOfPlayers = settings.NumberOfPlayers == default
                ? AnsiConsole.Ask<int>("How many players? (2-6): ")
                : settings.NumberOfPlayers;
            
            if (numberOfPlayers < 2 || numberOfPlayers > 6)
            {
                AnsiConsole.MarkupLine("[red]Invalid number of players. Must be between 2 and 6.[/]");
                return 1;
            }
            
            var playerIds = new List<(Guid id, string name)>();
            
            for (int i = 1; i <= numberOfPlayers; i++)
            {
                var playerName = AnsiConsole.Ask<string>($"Player {i} name: ");
                
                var joinRequest = new JoinGameRequest(
                    PlayerName: playerName,
                    BuyIn: createResponse.Configuration.StartingChips
                );
                
                var joinResponse = await apiClient.JoinGameAsync(createResponse.GameId, joinRequest);
                if (joinResponse == null)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to add player {playerName}.[/]");
                    return 1;
                }
                
                playerIds.Add((joinResponse.PlayerId, joinResponse.Name));
                AnsiConsole.MarkupLine($"[green]{joinResponse.Name}[/] joined (Position: {joinResponse.Position}, Chips: {joinResponse.ChipStack})");
            }
            
            // Step 3: Display game state
            Logger.Paragraph("Game Ready");
            
            var gameState = await apiClient.GetGameStateAsync(createResponse.GameId);
            if (gameState == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve game state.[/]");
                return 1;
            }
            
            DisplayGameState(gameState);
            
            // Phase 1 ends here - gameplay will be added in Phase 2
            AnsiConsole.MarkupLine("\n[yellow]Phase 1 Complete - Game is ready to start.[/]");
            AnsiConsole.MarkupLine("[dim]Start Hand, Betting, and Showdown will be available in Phase 2.[/]");
            
            return 0;
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Make sure the API server is running.[/]");
            return 1;
        }
    }
    
    private static void DisplayGameState(GetGameStateResponse gameState)
    {
        var table = new Table();
        table.AddColumn("Player");
        table.AddColumn("Position");
        table.AddColumn("Chips");
        table.AddColumn("Status");
        
        foreach (var player in gameState.Players.OrderBy(p => p.Position))
        {
            table.AddRow(
                player.Name,
                player.Position.ToString(),
                player.ChipStack.ToString(),
                player.Status
            );
        }
        
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[dim]Game Status: {gameState.Status}[/]");
    }
}
```

---

#### 4. Add API Play Settings

**File:** `CardGames.Poker.CLI/Play/ApiPlaySettings.cs`

```csharp
namespace CardGames.Poker.CLI.Play;

using Spectre.Console.Cli;
using System.ComponentModel;

/// <summary>
/// Settings for API-backed poker games.
/// </summary>
public class ApiPlaySettings : CommandSettings
{
    [CommandOption("-u|--api-url <URL>")]
    [Description("The base URL of the CardGames API")]
    public string? ApiUrl { get; init; }
    
    [CommandOption("-n|--players <COUNT>")]
    [Description("Number of players")]
    public int NumberOfPlayers { get; init; }
    
    [CommandOption("-a|--ante <AMOUNT>")]
    [Description("Ante amount")]
    public int Ante { get; init; }
    
    [CommandOption("-m|--min-bet <AMOUNT>")]
    [Description("Minimum bet amount")]
    public int MinBet { get; init; }
    
    [CommandOption("-c|--chips <AMOUNT>")]
    [Description("Starting chips per player")]
    public int StartingChips { get; init; }
}
```

---

#### 5. Update Program.cs to Register API Commands

**Changes to:** `CardGames.Poker.CLI/Program.cs`

Add a new branch for API-backed gameplay:

```csharp
// Add to configuration in CreateCommandApp()
configuration.AddBranch<ApiPlaySettings>("api", api =>
{
    api
        .AddCommand<ApiFiveCardDrawPlayCommand>("draw")
        .WithAlias("5cd")
        .WithAlias("5-card-draw")
        .WithDescription("Play 5-card Draw via the Web API.");
});
```

Add menu option in `RunInteractiveMenu()`:

```csharp
var mode = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("[green]What would you like to do?[/]")
        .AddChoices(
            "Play Poker (With Betting)",
            "Play via API (Multiplayer)",  // NEW OPTION
            "Deal Cards (Automated Dealer)",
            "Run Simulation (Manual Setup)",
            "Exit"));

// Add handler for new option
case "Play via API (Multiplayer)":
    RunApiPlayMenu();
    break;
```

Add new menu function:

```csharp
static void RunApiPlayMenu()
{
    var apiUrl = AnsiConsole.Ask("API URL:", "https://localhost:7034");
    
    var gameType = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select game type:[/]")
            .AddChoices("5-Card Draw", "Back"));
    
    if (gameType == "5-Card Draw")
    {
        var app = CreateCommandApp();
        app.Run(new[] { "api", "draw", "--api-url", apiUrl });
    }
}
```

---

#### 6. Update CLI Project File

**Changes to:** `CardGames.Poker.CLI/CardGames.Poker.CLI.csproj`

Add required package:

```xml
<ItemGroup>
    <PackageReference Include="System.Net.Http.Json" Version="9.0.0" />
</ItemGroup>
```

---

### CLI Integration Summary

| Change | File | Description |
|--------|------|-------------|
| New | `Api/ApiClient.cs` | HTTP client wrapper for API calls |
| New | `Api/ApiModels.cs` | DTOs for API communication |
| New | `Play/ApiFiveCardDrawPlayCommand.cs` | API-backed play command |
| New | `Play/ApiPlaySettings.cs` | Settings for API play mode |
| Modified | `Program.cs` | Register new commands and menu options |
| Modified | `CardGames.Poker.CLI.csproj` | Add HTTP client package |

---

## Implementation Order

### Step-by-Step Implementation

```
Phase 1 Implementation Order
============================

1. Domain Layer (1.1)
   ├── Create Enums/GameType.cs
   ├── Create Enums/GameStatus.cs
   ├── Create GameConfiguration.cs
   ├── Create Events/GameCreated.cs
   ├── Create Events/PlayerJoined.cs
   └── Create PokerGameAggregate.cs

2. Create Game Feature (1.2)
   ├── Create CreateGame/CreateGameRequest.cs
   ├── Create CreateGame/CreateGameResponse.cs
   ├── Create CreateGame/CreateGameValidator.cs
   ├── Create CreateGame/CreateGameEndpoint.cs
   └── Update MapFeatureEndpoints.cs

3. Join Game Feature (1.3)
   ├── Create JoinGame/JoinGameRequest.cs
   ├── Create JoinGame/JoinGameResponse.cs
   ├── Create JoinGame/JoinGameValidator.cs
   └── Create JoinGame/JoinGameEndpoint.cs

4. Get Game State Feature (1.4)
   ├── Create GetGameState/GetGameStateResponse.cs
   └── Create GetGameState/GetGameStateEndpoint.cs

5. Marten Configuration
   └── Update Program.cs with Marten event sourcing setup

6. CLI Integration
   ├── Create Api/ApiClient.cs
   ├── Create Api/ApiModels.cs
   ├── Create Play/ApiPlaySettings.cs
   ├── Create Play/ApiFiveCardDrawPlayCommand.cs
   ├── Update Program.cs
   └── Update CardGames.Poker.CLI.csproj
```

---

## Testing Strategy

### Unit Tests

**Test Project:** `Tests/CardGames.Poker.Api.Tests/`

#### Aggregate Tests
```csharp
public class PokerGameAggregateTests
{
    [Fact]
    public void Apply_GameCreated_SetsInitialState()
    {
        var aggregate = new PokerGameAggregate();
        var @event = new GameCreated(
            Guid.NewGuid(),
            GameType.FiveCardDraw,
            GameConfiguration.DefaultFiveCardDraw,
            DateTime.UtcNow);
        
        aggregate.Apply(@event);
        
        Assert.Equal(GameStatus.WaitingForPlayers, aggregate.Status);
        Assert.Empty(aggregate.Players);
    }
    
    [Fact]
    public void Apply_PlayerJoined_AddsPlayer()
    {
        // Arrange
        var aggregate = new PokerGameAggregate();
        aggregate.Apply(new GameCreated(...));
        
        // Act
        aggregate.Apply(new PlayerJoined(...));
        
        // Assert
        Assert.Single(aggregate.Players);
    }
    
    [Fact]
    public void Apply_SecondPlayerJoined_ChangesStatusToReadyToStart()
    {
        // Arrange + Act + Assert
    }
    
    [Fact]
    public void IsFull_ReturnsTrueWhenMaxPlayersReached()
    {
        // Test max player limit
    }
}
```

#### Validator Tests
```csharp
public class CreateGameValidatorTests
{
    [Fact]
    public void Validate_InvalidGameType_ReturnsError() { }
    
    [Fact]
    public void Validate_NegativeAnte_ReturnsError() { }
    
    [Fact]
    public void Validate_ValidRequest_Succeeds() { }
}

public class JoinGameValidatorTests
{
    [Fact]
    public void Validate_EmptyPlayerName_ReturnsError() { }
    
    [Fact]
    public void Validate_PlayerNameTooLong_ReturnsError() { }
    
    [Fact]
    public void Validate_NegativeBuyIn_ReturnsError() { }
}
```

### Integration Tests

**Test Project:** `Tests/CardGames.Poker.Api.Tests/`

Using Alba (Wolverine's test framework) for integration testing:

```csharp
public class GameEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    [Fact]
    public async Task CreateGame_ValidRequest_ReturnsCreatedGame()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { gameType = "FiveCardDraw" };
        
        // Act
        var response = await client.PostAsJsonAsync("/api/v1/games", request);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var game = await response.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(game);
        Assert.Equal("WaitingForPlayers", game.Status);
    }
    
    [Fact]
    public async Task JoinGame_ValidRequest_AddsPlayer()
    {
        // Create game, then join
    }
    
    [Fact]
    public async Task JoinGame_GameFull_ReturnsBadRequest()
    {
        // Create game, add max players, try to add one more
    }
    
    [Fact]
    public async Task GetGameState_ExistingGame_ReturnsState()
    {
        // Create game, get state
    }
    
    [Fact]
    public async Task GetGameState_NonExistentGame_ReturnsNotFound()
    {
        // Request non-existent game ID
    }
}
```

---

## Conclusion

This Phase 1 implementation plan provides:

1. **Complete domain model** with event-sourcing ready aggregates
2. **Three essential endpoints** for game lifecycle management
3. **Validation** using FluentValidation
4. **CLI integration** allowing API-backed gameplay
5. **Testing strategy** for both unit and integration tests

After Phase 1 is complete, the system will support:
- Creating poker games with custom configuration
- Players joining games
- Querying game state
- CLI interaction with the API

Phase 2 will build on this foundation to add:
- Hand lifecycle (Start Hand, Deal Cards)
- Betting actions
- Draw phase
- Showdown and winner determination
