# Follow the Queen PRD (Product Requirements Document) - Enhanced Version

## 1. Purpose
Add the **Follow the Queen** poker variant as a fully playable game in the existing CardGames system, with complete end-to-end support across API, background services, game flow, UI, and state rendering. This PRD provides **exhaustive implementation guidance** for an LLM developer to integrate the game into the current architecture and conventions.

---

## 2. Scope

### In Scope
- Full UI support for Follow the Queen in the Blazor client
- API endpoints and command/query handlers needed for a playable game
- Game flow integration (phases, dealing, betting, showdown)
- Table state rendering (public/private state) including wild card rules
- Background processing via existing game loop
- Game API routing and DI registration
- Refit interface generation for API client
- Game type registration and metadata discovery
- Consistent handling of game phases and special rules

### Out of Scope
- Non-poker game framework changes
- Major architecture redesign (e.g., ICardGame abstraction)
- Rewriting background services or table state builder for generic handling beyond required changes

---

## 3. Current State Analysis

### 3.1 Existing Domain Layer Components (Already Implemented)

| Component | File Path | Status |
|-----------|-----------|--------|
| `FollowTheQueenGame` | `src/CardGames.Poker/Games/FollowTheQueen/FollowTheQueenGame.cs` | ✅ Complete |
| `FollowTheQueenRules` | `src/CardGames.Poker/Games/FollowTheQueen/FollowTheQueenRules.cs` | ✅ Complete |
| `FollowTheQueenHand` | `src/CardGames.Poker/Hands/StudHands/FollowTheQueenHand.cs` | ✅ Complete |
| `FollowTheQueenGamePlayer` | `src/CardGames.Poker/Games/FollowTheQueen/FollowTheQueenGamePlayer.cs` | ✅ Complete |
| `FollowTheQueenShowdownResult` | `src/CardGames.Poker/Games/FollowTheQueen/FollowTheQueenShowdownResult.cs` | ✅ Complete |
| `FollowTheQueenWildCardRules` | `src/CardGames.Poker/Hands/WildCards/FollowTheQueenWildCardRules.cs` | ✅ Complete |

### 3.2 Existing API Layer Components

| Component | File Path | Status |
|-----------|-----------|--------|
| `FollowTheQueenFlowHandler` | `src/CardGames.Poker.Api/GameFlow/FollowTheQueenFlowHandler.cs` | ✅ Complete |
| `PokerGameMetadataRegistry.FollowTheQueenCode` | `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs` | ✅ Complete |
| `MapFeatureEndpoints` (uses reflection) | `src/CardGames.Poker.Api/Features/MapFeatureEndpoints.cs` | ✅ Auto-discovers |

### 3.3 Components Requiring Updates

| Component | File Path | Required Changes |
|-----------|-----------|------------------|
| `TableStateBuilder` | `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` | Add FTQ checks for Stud-style card handling, showdown, wild cards |
| `TablePlay.razor` | `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` | Add `IsFollowTheQueen` property, wild card banner |
| `Program.cs` (Web) | `src/CardGames.Poker.Web/Program.cs` | Register Refit client and wrapper |

### 3.4 New Components Required

| Component | File Path | Description |
|-----------|-----------|-------------|
| API Endpoints | `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/` | Full endpoint folder structure |
| Refit Interface | `src/CardGames.Contracts/RefitInterface.v1.cs` | Add `IFollowTheQueenApi` interface |
| API Client Wrapper | `src/CardGames.Poker.Web/Services/GameApi/FollowTheQueenApiClientWrapper.cs` | Wrapper implementing `IGameApiClient` |

---

## 4. Goals and Success Criteria

### Goals
1. Follow the Queen is selectable and playable in the UI
2. Players can start a hand, bet through all streets (Third–Seventh), and complete showdown
3. Wild card rules (Queens + "following" rank) are displayed in UI and API responses
4. Phases and card visibility are correct for all players
5. Background automation works with Follow the Queen's phase flow

### Success Criteria
- [x] Game appears in available games list with correct metadata
- [ ] A hand can be completed without errors in automated loop or manual UI flow
- [ ] Table state shows correct cards and hand descriptions for all phases
- [ ] No fallback to Five Card Draw handlers or UI flows when Follow the Queen is active
- [ ] Wild card rules update dynamically when Queens are dealt face-up

---

## 5. User Stories

1. **As a player**, I can choose Follow the Queen from the available games list.
2. **As a player**, I can start and complete a hand in Follow the Queen using the UI.
3. **As a player**, I can see my own hole cards (face-down) and others' visible board cards (face-up) correctly.
4. **As a player**, I can see the wild card rule for the current hand (Queens always wild + following rank).
5. **As a player**, I can view showdown results with correct hand descriptions that account for wild cards.
6. **As a player**, I can see the "bring-in" player and betting flow proceed correctly through all streets.

---

## 6. Detailed Implementation Guide

### 6.1 API Endpoints (New Feature Folder)

#### 6.1.1 Create Feature Folder Structure

Create the following folder structure under `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/`:

```
FollowTheQueen/
├── FollowTheQueenApiMapGroup.cs
├── Feature.cs (optional constants)
└── v1/
    ├── V1.cs
    ├── Commands/
    │   ├── StartHand/
    │   │   ├── StartHandEndpoint.cs
    │   │   ├── StartHandCommand.cs
    │   │   ├── StartHandCommandHandler.cs
    │   │   └── StartHandSuccessful.cs
    │   ├── CollectAntes/
    │   │   ├── CollectAntesEndpoint.cs
    │   │   ├── CollectAntesCommand.cs
    │   │   ├── CollectAntesCommandHandler.cs
    │   │   └── CollectAntesSuccessful.cs
    │   ├── DealHands/
    │   │   ├── DealHandsEndpoint.cs
    │   │   ├── DealHandsCommand.cs
    │   │   ├── DealHandsCommandHandler.cs
    │   │   └── DealHandsSuccessful.cs
    │   ├── ProcessBettingAction/
    │   │   ├── ProcessBettingActionEndpoint.cs
    │   │   ├── ProcessBettingActionCommand.cs
    │   │   ├── ProcessBettingActionCommandHandler.cs
    │   │   └── ProcessBettingActionSuccessful.cs
    │   └── PerformShowdown/
    │       ├── PerformShowdownEndpoint.cs
    │       ├── PerformShowdownCommand.cs
    │       ├── PerformShowdownCommandHandler.cs
    │       └── PerformShowdownSuccessful.cs
    └── Queries/
        └── GetCurrentPlayerTurn/
            ├── GetCurrentPlayerTurnEndpoint.cs
            ├── GetCurrentPlayerTurnQuery.cs
            ├── GetCurrentPlayerTurnQueryHandler.cs
            └── GetCurrentPlayerTurnResponse.cs
```

#### 6.1.2 FollowTheQueenApiMapGroup.cs

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/FollowTheQueenApiMapGroup.cs`

```csharp
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen;

[EndpointMapGroup]
public static class FollowTheQueenApiMapGroup
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var followTheQueen = app.NewVersionedApi("FollowTheQueen");
        followTheQueen.MapV1();
    }
}
```

**Note:** The `[EndpointMapGroup]` attribute is discovered by reflection in `MapFeatureEndpoints.cs`, so **no changes to `MapFeatureEndpoints.cs` are required**.

#### 6.1.3 V1.cs

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/V1.cs`

```csharp
using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Queries.GetCurrentPlayerTurn;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1;

public static class V1
{
    public static void MapV1(this IVersionedEndpointRouteBuilder app)
    {
        var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/follow-the-queen")
            .HasApiVersion(1.0)
            .WithTags([Feature.Name])
            .AddFluentValidationAutoValidation();

        mapGroup
            .MapStartHand()
            .MapCollectAntes()
            .MapDealHands()
            .MapProcessBettingAction()
            .MapPerformShowdown()
            .MapGetCurrentPlayerTurn();
    }
}
```

#### 6.1.4 Feature.cs

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/Feature.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.FollowTheQueen;

public static class Feature
{
    public const string Name = "Follow the Queen";
}
```

#### 6.1.5 Command Handlers Pattern

Each command handler should follow the pattern established by `SevenCardStud`. Here is the **StartHand** example:

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/StartHand/StartHandCommand.cs`

```csharp
using CardGames.Poker.Api.Infrastructure;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

public record StartHandCommand(Guid GameId) : IRequest<IResult>, IGameStateChangingCommand
{
    public Guid GetGameId() => GameId;
}
```

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/StartHand/StartHandCommandHandler.cs`

```csharp
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

public sealed class StartHandCommandHandler(CardsDbContext context) : IRequestHandler<StartHandCommand, IResult>
{
    public async Task<IResult> Handle(StartHandCommand request, CancellationToken cancellationToken)
    {
        var game = await context.Games
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
            .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

        if (game is null)
        {
            return Results.NotFound(new { Message = "Game not found" });
        }

        // Verify this is a Follow the Queen game
        if (!string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { Message = "This endpoint is only for Follow the Queen games" });
        }

        // Verify game is in a state where a new hand can start
        if (game.CurrentPhase != nameof(Phases.WaitingToStart) &&
            game.CurrentPhase != nameof(Phases.Complete) &&
            game.CurrentPhase != nameof(Phases.WaitingForPlayers))
        {
            return Results.Conflict(new { Message = $"Cannot start hand in phase {game.CurrentPhase}" });
        }

        // Reset for new hand
        game.CurrentHandNumber++;
        game.CurrentPhase = nameof(Phases.CollectingAntes);
        game.CurrentPlayerIndex = -1;
        game.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var player in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
        {
            player.CurrentBet = 0;
            player.TotalContributedThisHand = 0;
            player.HasFolded = player.IsSittingOut;
            player.IsAllIn = false;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(new StartHandSuccessful { GameId = game.Id, HandNumber = game.CurrentHandNumber });
    }
}
```

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/StartHand/StartHandEndpoint.cs`

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

public static class StartHandEndpoint
{
    public static IEndpointRouteBuilder MapStartHand(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{gameId:guid}/hands", async (
            [FromRoute] Guid gameId,
            [FromServices] IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new StartHandCommand(gameId);
            return await mediator.Send(command, cancellationToken);
        })
        .WithName("FollowTheQueenStartHand")
        .WithSummary("Start Hand")
        .WithDescription("Starts a new hand of Follow the Queen poker.")
        .Produces<StartHandSuccessful>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
```

**File:** `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/StartHand/StartHandSuccessful.cs`

```csharp
namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

public record StartHandSuccessful
{
    public Guid GameId { get; init; }
    public int HandNumber { get; init; }
}
```

#### 6.1.6 DealHands Handler (Third Street)

This is **critical** for Follow the Queen - it must deal 2 hole cards + 1 board card and track face-up cards for wild card determination.

**Key Implementation Points:**
- Deal order: 2 hole cards (face-down) + 1 board card (face-up) per player
- Track all face-up cards in order via `GameCard.DealtAtPhase` and `GameCard.IsVisible`
- Determine bring-in player (lowest visible card)
- Transition to `ThirdStreet` phase (betting)

The `FollowTheQueenFlowHandler.DealCardsAsync` method already implements this logic. The endpoint handler should delegate to it or use similar logic.

#### 6.1.7 ProcessBettingAction Handler

Follow the same pattern as `SevenCardStud`. Key differences:
- Min bet sizes: Small bet for Third/Fourth Street, Big bet for Fifth/Sixth/Seventh Street
- Best visible hand determines first actor (except Third Street with bring-in)

---

### 6.2 Refit Interface Updates

#### 6.2.1 Add IFollowTheQueenApi Interface

**File:** `src/CardGames.Contracts/RefitInterface.v1.cs`

Add the following interface (after `ISevenCardStudApi`):

```csharp
/// <summary>
/// Refit API client interface for Follow the Queen endpoints.
/// </summary>
public interface IFollowTheQueenApi
{
    /// <summary>Start Hand</summary>
    /// <remarks>Starts a new hand of Follow the Queen poker.</remarks>
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/follow-the-queen/{gameId}/hands")]
    Task<IApiResponse<StartHandSuccessful>> FollowTheQueenStartHandAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Collect Antes</summary>
    /// <remarks>Collects the mandatory ante bet from all players.</remarks>
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/follow-the-queen/{gameId}/hands/antes")]
    Task<IApiResponse<CollectAntesSuccessful>> FollowTheQueenCollectAntesAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Deal Hands</summary>
    /// <remarks>Deals Third Street cards (2 hole + 1 board) to all players.</remarks>
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/follow-the-queen/{gameId}/hands/deal")]
    Task<IApiResponse<DealHandsSuccessful>> FollowTheQueenDealHandsAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Process Betting Action</summary>
    /// <remarks>Processes a betting action from the current player.</remarks>
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/follow-the-queen/{gameId}/betting")]
    Task<IApiResponse<ProcessBettingActionSuccessful>> FollowTheQueenProcessBettingActionAsync(Guid gameId, [Body] ProcessBettingActionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Perform Showdown</summary>
    /// <remarks>Performs the showdown and awards the pot to the winner(s).</remarks>
    [Headers("Accept: application/json, application/problem+json")]
    [Post("/api/v1/games/follow-the-queen/{gameId}/showdown")]
    Task<IApiResponse<PerformShowdownSuccessful>> FollowTheQueenPerformShowdownAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Get Current Player Turn</summary>
    /// <remarks>Gets information about whose turn it is and available actions.</remarks>
    [Headers("Accept: application/json")]
    [Get("/api/v1/games/follow-the-queen/{gameId}/current-turn")]
    Task<IApiResponse<GetCurrentPlayerTurnResponse>> FollowTheQueenGetCurrentPlayerTurnAsync(Guid gameId, CancellationToken cancellationToken = default);
}
```

**Note:** After adding the interface, regenerate the Refit contracts or ensure the build picks up the new interface.

---

### 6.3 Web Client Updates

#### 6.3.1 Create FollowTheQueenApiClientWrapper

**File:** `src/CardGames.Poker.Web/Services/GameApi/FollowTheQueenApiClientWrapper.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class FollowTheQueenApiClientWrapper(IFollowTheQueenApi client) : IGameApiClient
{
    public string GameTypeCode => "FOLLOWTHEQUEEN";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        var startHandResponse = await client.FollowTheQueenStartHandAsync(gameId);
        if (!startHandResponse.IsSuccessStatusCode) return false;

        var collectAntesResponse = await client.FollowTheQueenCollectAntesAsync(gameId);
        if (!collectAntesResponse.IsSuccessStatusCode) return false;

        var dealHandsResponse = await client.FollowTheQueenDealHandsAsync(gameId);
        return dealHandsResponse.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
    {
        return await client.FollowTheQueenProcessBettingActionAsync(gameId, request);
    }

    public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        // Follow the Queen is a stud game - no draw phase
        return Task.FromResult(new ProcessDrawResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Draw phase not supported for Follow the Queen." 
        });
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.FollowTheQueenPerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        // Follow the Queen does not have Drop or Stay
        return Task.FromResult(false);
    }
}
```

#### 6.3.2 Update Program.cs - Register Refit Client

**File:** `src/CardGames.Poker.Web/Program.cs`

Add the following after the `ISevenCardStudApi` registration (around line 75):

```csharp
builder.Services
    .AddRefitClient<IFollowTheQueenApi>(
        settingsAction: _ => new RefitSettings(),
        httpClientName: "followTheQueenApi")
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri("https+http://api");
        c.Timeout = TimeSpan.FromSeconds(600);
    })
    .AddHttpMessageHandler<AuthenticationStateHandler>();
```

#### 6.3.3 Update Program.cs - Register Client Wrapper

Add to the client wrapper registrations (around line 137):

```csharp
builder.Services.AddScoped<IGameApiClient, FollowTheQueenApiClientWrapper>();
```

**Complete diff for Program.cs:**

```csharp
// After ISevenCardStudApi registration:
builder.Services
    .AddRefitClient<IFollowTheQueenApi>(
        settingsAction: _ => new RefitSettings(),
        httpClientName: "followTheQueenApi")
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri("https+http://api");
        c.Timeout = TimeSpan.FromSeconds(600);
    })
    .AddHttpMessageHandler<AuthenticationStateHandler>();

// In the IGameApiClient registrations section:
builder.Services.AddScoped<IGameApiClient, FiveCardDrawApiClientWrapper>();
builder.Services.AddScoped<IGameApiClient, SevenCardStudApiClientWrapper>();
builder.Services.AddScoped<IGameApiClient, KingsAndLowsApiClientWrapper>();
builder.Services.AddScoped<IGameApiClient, TwosJacksManWithTheAxeApiClientWrapper>();
builder.Services.AddScoped<IGameApiClient, FollowTheQueenApiClientWrapper>(); // ADD THIS LINE
```

---

### 6.4 TableStateBuilder Updates

#### 6.4.1 Update BuildPublicStateAsync - Stud Game Detection

**File:** `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

**Location:** Line ~65-75, modify the `isSevenCardStudGame` check:

```csharp
// BEFORE:
var isSevenCardStudGame = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase);

// AFTER:
var isSevenCardStudGame = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
```

#### 6.4.2 Update BuildPrivateStateAsync - Hand Evaluation

**Location:** Line ~295-360, add Follow the Queen handling after Baseball:

```csharp
// After the isBaseballGame block, add:
var isFollowTheQueen = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);

if (isFollowTheQueen)
{
    var holeCards = playerCardEntities.Where(c => c.Location == CardLocation.Hole).ToList();
    var boardCards = playerCardEntities.Where(c => c.Location == CardLocation.Board).ToList();

    if (playerCardEntities.Count >= 3) // At least Third Street dealt
    {
        var initialHoleCards = holeCards.Take(2)
            .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
            .ToList();
        var openCards = boardCards
            .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
            .ToList();

        // Get face-up cards for wild card determination
        var faceUpCardsInOrder = await _context.GameCards
            .Where(c => c.GameId == game.Id &&
                        c.HandNumber == game.CurrentHandNumber &&
                        c.IsVisible &&
                        !c.IsDiscarded)
            .OrderBy(c => c.DealOrder)
            .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
            .ToListAsync(cancellationToken);

        if (holeCards.Count >= 3 && initialHoleCards.Count == 2 && openCards.Count <= 4)
        {
            var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
            var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
            handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(ftqHand);
        }
        else if (initialHoleCards.Count >= 2)
        {
            // Partial hand (before 7th street)
            var allHoleCards = holeCards
                .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
                .ToList();
            var studHand = new StudHand(initialHoleCards, openCards, allHoleCards.Skip(2).ToList());
            handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
        }
    }
}
```

**Add the using statement at the top of the file:**
```csharp
using CardGames.Poker.Hands.StudHands;
```

#### 6.4.3 Update BuildShowdownPublicDtoAsync

**Location:** Line ~720-740, add Follow the Queen check:

```csharp
// After the isKingsAndLows declaration:
var isFollowTheQueen = string.Equals(
    game.GameType?.Code,
    PokerGameMetadataRegistry.FollowTheQueenCode,
    StringComparison.OrdinalIgnoreCase);
```

**Location:** Line ~760-800, add Follow the Queen hand creation:

```csharp
// After the isKingsAndLows block in the foreach loop:
else if (isFollowTheQueen)
{
    var holeCards = cards
        .Where(c => c.Location == CardLocation.Hole)
        .OrderBy(c => c.DealOrder)
        .ToList();
    var boardCards = cards
        .Where(c => c.Location == CardLocation.Board)
        .OrderBy(c => c.DealOrder)
        .ToList();

    var initialHoleCards = holeCards.Take(2)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();
    var openCards = boardCards
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();

    // Get face-up cards for wild card determination
    var faceUpCardsInOrder = await _context.GameCards
        .Where(c => c.GameId == game.Id &&
                    c.HandNumber == game.CurrentHandNumber &&
                    c.IsVisible &&
                    !c.IsDiscarded)
        .OrderBy(c => c.DealOrder)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToListAsync(cancellationToken);

    if (initialHoleCards.Count == 2 && openCards.Count <= 4 && holeCards.Count >= 3)
    {
        var downCard = new Card((Suit)holeCards[2].Suit, (Symbol)holeCards[2].Symbol);
        var ftqHand = new FollowTheQueenHand(initialHoleCards, openCards, downCard, faceUpCardsInOrder);
        var wildCards = ftqHand.WildCards;
        var wildIndexes = new List<int>();
        for (int i = 0; i < coreCards.Count; i++)
        {
            if (wildCards.Contains(coreCards[i]))
            {
                wildIndexes.Add(i);
            }
        }
        playerHandEvaluations[gp.Player.Name] = (ftqHand, null, null, null, gp, cards, wildIndexes);
    }
}
```

#### 6.4.4 Update BuildSeatPublicDto - Stud Card Visibility

**Location:** Line ~490-510, update the stud game check:

```csharp
// BEFORE:
var isSevenCardStud = string.Equals(gameTypeCode, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase);

// AFTER:
var isSevenCardStud = string.Equals(gameTypeCode, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
```

#### 6.4.5 Update BuildPrivateHand - Stud Card Ordering

**Location:** Line ~600-605, update the stud game check:

```csharp
// BEFORE:
var isSevenCardStud = string.Equals(gameTypeCode, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase);

// AFTER:
var isSevenCardStud = string.Equals(gameTypeCode, PokerGameMetadataRegistry.SevenCardStudCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase);
```

#### 6.4.6 Update BuildWildCardRulesDto (If Present)

If there is a `BuildWildCardRulesDto` method or similar, add Follow the Queen wild card info:

```csharp
if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode, StringComparison.OrdinalIgnoreCase))
{
    // Queens are always wild
    var wildDescription = "Queens are Wild";
    
    // Get face-up cards to determine following rank
    var faceUpCards = await _context.GameCards
        .Where(c => c.GameId == game.Id &&
                    c.HandNumber == game.CurrentHandNumber &&
                    c.IsVisible &&
                    !c.IsDiscarded)
        .OrderBy(c => c.DealOrder)
        .ToListAsync(cancellationToken);

    // Find the card that follows the last Queen
    Symbol? followingRank = null;
    for (int i = 0; i < faceUpCards.Count - 1; i++)
    {
        if (faceUpCards[i].Symbol == CardSymbol.Queen)
        {
            var nextCard = faceUpCards[i + 1];
            if (nextCard.Symbol != CardSymbol.Queen)
            {
                followingRank = (Symbol)nextCard.Symbol;
            }
        }
    }

    if (followingRank.HasValue)
    {
        wildDescription += $" + {followingRank.Value}s are Wild";
    }

    return new WildCardRulesDto
    {
        HasWildCards = true,
        WildCardDescription = wildDescription
    };
}
```

---

### 6.5 TablePlay.razor Updates

#### 6.5.1 Add IsFollowTheQueen Property

**File:** `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`

**Location:** Around line 526, add after `IsBaseball`:

```csharp
private bool IsFollowTheQueen => string.Equals(_gameTypeCode, "FOLLOWTHEQUEEN", StringComparison.OrdinalIgnoreCase);
```

#### 6.5.2 Update Stud Card Display Checks

Search for all occurrences of `IsSevenCardStud` and update to include Follow the Queen:

**Example pattern:**
```csharp
// BEFORE:
if (IsSevenCardStud || IsBaseball)

// AFTER:
if (IsSevenCardStud || IsBaseball || IsFollowTheQueen)
```

**OR better approach - create a helper property:**
```csharp
private bool IsStudStyleGame => IsSevenCardStud || IsBaseball || IsFollowTheQueen;
```

Then replace all `(IsSevenCardStud || IsBaseball)` checks with `IsStudStyleGame`.

#### 6.5.3 Add Wild Card Display Banner

Add a wild card banner component that displays when Follow the Queen is active:

```razor
@* Wild Card Rules Banner (for Follow the Queen) *@
@if (IsFollowTheQueen && _tableState?.SpecialRules?.HasWildCards == true)
{
    <div class="wild-card-banner">
        <span class="wild-icon">🃏</span>
        <span class="wild-text">@(_tableState?.SpecialRules?.WildCardDescription ?? "Queens are Wild")</span>
    </div>
}
```

**CSS for banner (in `TablePlay.razor.css`):**
```css
.wild-card-banner {
    position: absolute;
    top: 10px;
    left: 50%;
    transform: translateX(-50%);
    background: linear-gradient(135deg, #6b21a8, #9333ea);
    color: white;
    padding: 8px 16px;
    border-radius: 8px;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 8px;
    box-shadow: 0 4px 12px rgba(107, 33, 168, 0.4);
    z-index: 100;
}

.wild-icon {
    font-size: 1.5em;
}

.wild-text {
    font-size: 0.95rem;
}
```

---

### 6.6 Data Contracts Updates

#### 6.6.1 Ensure WildCardRulesDto Supports Dynamic Description

**File:** `src/CardGames.Contracts/SignalR/WildCardRulesDto.cs` (or wherever defined)

Ensure the DTO has:
```csharp
public class WildCardRulesDto
{
    public bool HasWildCards { get; set; }
    public string? WildCardDescription { get; set; }
    public List<string>? WildRanks { get; set; } // Optional: specific wild ranks
}
```

---

## 7. Implementation Order

Execute these steps in order to minimize integration issues:

### Phase 1: API Layer (Estimated: 2-3 hours)
1. ✅ Verify `FollowTheQueenFlowHandler` exists and is correct
2. Create API endpoint folder structure (`Features/Games/FollowTheQueen/`)
3. Implement all command handlers following SevenCardStud patterns
4. Add `IFollowTheQueenApi` to RefitInterface.v1.cs
5. Build and verify API starts without errors

### Phase 2: Table State Builder (Estimated: 1-2 hours)
1. Update all `isSevenCardStud` checks to include `FollowTheQueenCode`
2. Add `FollowTheQueenHand` evaluation in `BuildPrivateStateAsync`
3. Add `FollowTheQueenHand` showdown evaluation in `BuildShowdownPublicDtoAsync`
4. Add wild card rules extraction for Follow the Queen
5. Test with API requests to verify state is built correctly

### Phase 3: Web Client (Estimated: 1 hour)
1. Create `FollowTheQueenApiClientWrapper.cs`
2. Register Refit client in `Program.cs`
3. Register client wrapper in `Program.cs`
4. Update `TablePlay.razor`:
   - Add `IsFollowTheQueen` property
   - Update stud card display checks
   - Add wild card banner

### Phase 4: Testing (Estimated: 1-2 hours)
1. Create a new Follow the Queen game from UI
2. Start a hand and verify:
   - Ante collection works
   - Third Street dealing shows correct cards
   - Wild card banner displays correctly
   - Betting actions process correctly
   - Phase transitions work (Third → Fourth → ... → Showdown)
   - Showdown evaluates hands with wild cards correctly

---

## 8. Testing Requirements

### 8.1 Unit Tests

**File:** `tests/CardGames.Poker.Tests/Hands/StudHands/FollowTheQueenHandTests.cs`

Ensure tests cover:
- Basic hand evaluation without wild cards
- Queens as wild cards
- Following rank wild cards (card after face-up Queen)
- Multiple Queens dealt (last one determines following rank)
- Edge case: No Queens dealt (only standard hand evaluation)

### 8.2 Integration Tests

Create integration tests for:
- `FollowTheQueenFlowHandler` phase transitions
- API endpoint round-trip (start hand → complete showdown)
- Table state builder output for Follow the Queen games

### 8.3 Manual Testing Checklist

- [ ] Game appears in available games dropdown
- [ ] Can create a new Follow the Queen table
- [ ] Hand starts and collects antes
- [ ] Third Street deals correctly (2 hole + 1 board per player)
- [ ] Bring-in player is determined (lowest visible card)
- [ ] Betting actions work on all streets
- [ ] Fourth-Sixth Street deals one face-up card each
- [ ] Seventh Street deals one face-down card
- [ ] Wild card banner displays and updates correctly
- [ ] Showdown evaluates hands with wild cards
- [ ] Pot is awarded correctly
- [ ] Continuous play starts next hand automatically

---

## 9. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Wild card logic is complex | High | Use existing `FollowTheQueenWildCardRules` class; extensive unit tests |
| TableStateBuilder has many code paths | Medium | Carefully follow existing patterns for SevenCardStud/Baseball |
| Face-up card tracking across streets | High | Ensure `DealOrder` and `DealtAtPhase` are set correctly in `DealCardsAsync` |
| Bring-in player determination | Medium | Copy logic from `SevenCardStudFlowHandler` |
| UI not updating wild card display | Medium | Ensure SignalR broadcasts include updated `SpecialRules` |

---

## 10. Dependencies

- `CardGames.Poker.Games.FollowTheQueen.FollowTheQueenGame` ✅
- `CardGames.Poker.Hands.StudHands.FollowTheQueenHand` ✅
- `CardGames.Poker.Hands.WildCards.FollowTheQueenWildCardRules` ✅
- `CardGames.Poker.Games.FollowTheQueen.FollowTheQueenRules` ✅
- `CardGames.Poker.Api.GameFlow.FollowTheQueenFlowHandler` ✅
- `CardGames.Poker.Api.Games.PokerGameMetadataRegistry.FollowTheQueenCode` ✅

---

## 11. Acceptance Criteria

1. **Game Discovery**: Follow the Queen appears in the "Available Poker Games" list with correct name, description, and player limits (2-7 players).

2. **Game Creation**: A user can create a new Follow the Queen table with custom ante/bet settings.

3. **Hand Lifecycle**: A complete hand can be played through all phases:
   - CollectingAntes → ThirdStreet → FourthStreet → FifthStreet → SixthStreet → SeventhStreet → Showdown → Complete

4. **Card Display**: 
   - Hole cards (face-down) are visible only to the owning player
   - Board cards (face-up) are visible to all players
   - Card ordering follows correct stud display order

5. **Wild Card Display**: The UI shows current wild card rules (Queens + following rank if applicable).

6. **Hand Evaluation**: Showdown correctly evaluates hands using wild card substitution.

7. **Continuous Play**: After hand completion, the next hand starts automatically with correct dealer rotation.

8. **No Regressions**: Existing games (Five Card Draw, Seven Card Stud, Baseball, etc.) continue to work correctly.

---

## 12. Appendix: File Locations Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/FollowTheQueenApiMapGroup.cs` | Endpoint discovery |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/Feature.cs` | Constants |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/V1.cs` | Version mapping |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/StartHand/*` | Start hand endpoint |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/CollectAntes/*` | Collect antes endpoint |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/DealHands/*` | Deal hands endpoint |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/ProcessBettingAction/*` | Betting endpoint |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/*` | Showdown endpoint |
| `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Queries/GetCurrentPlayerTurn/*` | Current player query |
| `src/CardGames.Poker.Web/Services/GameApi/FollowTheQueenApiClientWrapper.cs` | API client wrapper |

### Files to Modify

| File | Changes |
|------|---------|
| `src/CardGames.Contracts/RefitInterface.v1.cs` | Add `IFollowTheQueenApi` |
| `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` | Add FTQ checks (5 locations) |
| `src/CardGames.Poker.Web/Program.cs` | Register Refit client + wrapper |
| `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` | Add FTQ property + UI updates |
| `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor.css` | Wild card banner styles |

---

## 13. Glossary

| Term | Definition |
|------|------------|
| Third Street | Initial dealing round: 2 hole cards + 1 board card |
| Bring-in | Forced bet by player with lowest visible card on Third Street |
| Following Rank | The rank of the card dealt immediately after a face-up Queen |
| Stud Game | Poker variant where players receive a mix of face-up and face-down cards |
| Wild Card | A card that can represent any rank/suit for hand evaluation |
