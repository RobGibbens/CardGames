# Baseball Poker - Product Requirements Document (PRD)

## Document Metadata
- **Version:** 1.0
- **Created:** 2025-01-29
- **Game Type Code:** `BASEBALL`
- **Status:** Implementation Ready

---

## 1. Executive Summary

This PRD specifies the complete implementation requirements for adding **Baseball Poker** to the CardGames UI and API. Baseball is a Seven Card Stud variant with wild cards (3s and 9s) and a unique **buy-card mechanic** (receiving a 4 face-up allows the player to pay to receive an additional hole card).

### 1.1 Current State
The domain layer (`CardGames.Poker`) already contains a complete `BaseballGame` class implementation including:
- Game logic and rules
- `BaseballGamePlayer` with buy-card tracking
- `BaseballHand` evaluation with wild card support
- `BuyCardResult` and `BaseballShowdownResult` types
- `PokerGameMetadataAttribute` decoration for auto-discovery

### 1.2 Implementation Gap
Baseball is **not yet integrated** into:
- API endpoints for game flow commands
- Game flow handler for the background service
- Blazor UI for rendering and player interactions
- Buy-card phase handling (unique to Baseball)

---

## 2. Baseball Game Rules Summary

### 2.1 Basic Structure
- **Type:** Seven Card Stud variant
- **Players:** 2-6 players (per metadata: 2-4, configurable)
- **Cards:** 7 cards total per player (2 hole + 4 up + 1 down), but may receive more via buy-card
- **Hand:** Best 5-card hand from all cards wins

### 2.2 Wild Cards
- **3s** are always wild
- **9s** are always wild
- Wild cards can represent any card to make the best possible hand

### 2.3 Buy-Card Mechanic (Critical Feature)
When a player is dealt a **4 face-up** (on any street):
1. The game pauses for that player
2. Player is offered the choice to "buy" an extra face-down card
3. The buy-card price goes directly to the pot (it is NOT a bet)
4. If accepted: player receives one additional hole card
5. If declined: play continues normally
6. **Important:** Cards bought are always face-down, so they do not trigger additional buy offers

### 2.4 Phase Flow
```
WaitingToStart → CollectingAntes → ThirdStreet → FourthStreet → FifthStreet → SixthStreet → SeventhStreet → Showdown → Complete
```

#### Phase Details:
| Phase | Cards Dealt | Visibility | Betting | Notes |
|-------|-------------|------------|---------|-------|
| CollectingAntes | 0 | N/A | Antes collected | All players |
| ThirdStreet | 2 hole + 1 board | 2 down, 1 up | Small bet (bring-in) | Buy-card possible |
| FourthStreet | 1 board | Face up | Small bet | Buy-card possible |
| FifthStreet | 1 board | Face up | Big bet | Buy-card possible |
| SixthStreet | 1 board | Face up | Big bet | Buy-card possible |
| SeventhStreet | 1 hole | Face down | Big bet | No buy-card (down card) |
| Showdown | 0 | All revealed | N/A | Best 5-card hand wins |

### 2.5 Betting Structure
- **Antes:** Collected before dealing
- **Bring-in:** Lowest visible card on Third Street (optional, configurable)
- **Small bet:** Third and Fourth Street
- **Big bet:** Fifth, Sixth, and Seventh Street

---

## 3. Implementation Requirements

### 3.1 File Changes Overview

| Layer | File/Folder | Action | Priority |
|-------|-------------|--------|----------|
| **API Registry** | `PokerGameMetadataRegistry.cs` | Add constant (backward compat) | P1 |
| **API Flow Handler** | `GameFlow/BaseballFlowHandler.cs` | Create new | P1 |
| **API Endpoints** | `Features/Games/Baseball/*` | Create new folder structure | P1 |
| **API TableStateBuilder** | `Services/TableStateBuilder.cs` | Add Baseball hand evaluation | P1 |
| **API Background Service** | `Services/ContinuousPlayBackgroundService.cs` | Add buy-card phase handling | P1 |
| **Contracts** | `RefitInterface.v1.cs` | Regenerate after OpenAPI update | P2 |
| **Blazor UI** | `TablePlay.razor` | Add Baseball-specific UI | P2 |
| **Blazor API Client** | `Services/GameApi/BaseballApiClientWrapper.cs` | Create new | P2 |
| **Blazor DI** | `Program.cs` | Register Baseball client | P2 |
| **Blazor Overlay** | `Components/Shared/BuyCardOverlay.razor` | Create new | P2 |

---

## 4. API Layer Implementation

### 4.1 Add Registry Constant

**File:** `CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`

Add a constant for backward compatibility with existing code patterns:

```csharp
// Add after line 24 (after FollowTheQueenCode)
public const string BaseballCode = "BASEBALL";
```

**Note:** The registry uses reflection-based auto-discovery, so `BaseballGame` is already registered via its `PokerGameMetadataAttribute`. The constant is for code references.

---

### 4.2 Create Game Flow Handler

**File:** `CardGames.Poker.Api/GameFlow/BaseballFlowHandler.cs` (NEW)

Create a new flow handler following the `SevenCardStudFlowHandler` pattern but with buy-card support:

```csharp
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.Baseball;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Baseball poker.
/// </summary>
/// <remarks>
/// Baseball is a seven-card stud variant with:
/// - 3s and 9s are wild
/// - When dealt a 4 face-up, player may pay to receive an extra hole card
/// </remarks>
public sealed class BaseballFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "BASEBALL";

    /// <inheritdoc />
    public override IReadOnlyList<string> SpecialPhases => [nameof(Phases.BuyCardOffer)];

    /// <inheritdoc />
    public override GameRules GetGameRules()
    {
        // Create rules from the domain class
        var game = new BaseballGame();
        return game.GetGameRules();
    }

    /// <inheritdoc />
    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.StreetBased,
            DealingRounds =
            [
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.ThirdStreet),
                    HoleCards = 2,
                    BoardCards = 1,
                    HasBettingAfter = true,
                    HasBuyCardCheck = true  // New property needed
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FourthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true,
                    HasBuyCardCheck = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FifthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true,
                    HasBuyCardCheck = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SixthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true,
                    HasBuyCardCheck = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SeventhStreet),
                    HoleCards = 1,
                    BoardCards = 0,
                    HasBettingAfter = true,
                    HasBuyCardCheck = false  // Down card, no buy
                }
            ]
        };
    }

    /// <inheritdoc />
    public override string? GetNextPhase(Game game, string currentPhase)
    {
        if (IsSinglePlayerRemaining(game) && !IsResolutionPhase(currentPhase))
        {
            return nameof(Phases.Showdown);
        }

        // Handle BuyCardOffer -> return to current street
        if (currentPhase == nameof(Phases.BuyCardOffer))
        {
            // Return to the street phase stored in game state
            return game.PreviousPhase ?? nameof(Phases.ThirdStreet);
        }

        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
            nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
            nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
            nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
            nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
            nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    // Additional methods for dealing, showdown, buy-card handling...
    // See Section 4.6 for complete implementation
}
```

---

### 4.3 Create API Feature Folder Structure

Create the following folder structure under `CardGames.Poker.Api/Features/Games/Baseball/`:

```
Baseball/
├── BaseballApiMapGroup.cs
└── v1/
    ├── Feature.cs
    ├── V1.cs
    ├── Commands/
    │   ├── StartHand/
    │   │   ├── StartHandCommand.cs
    │   │   ├── StartHandCommandHandler.cs
    │   │   ├── StartHandEndpoint.cs
    │   │   └── StartHandSuccessful.cs
    │   ├── CollectAntes/
    │   │   ├── CollectAntesCommand.cs
    │   │   ├── CollectAntesCommandHandler.cs
    │   │   ├── CollectAntesEndpoint.cs
    │   │   └── CollectAntesSuccessful.cs
    │   ├── DealHands/
    │   │   ├── DealHandsCommand.cs
    │   │   ├── DealHandsCommandHandler.cs
    │   │   ├── DealHandsEndpoint.cs
    │   │   └── DealHandsSuccessful.cs
    │   ├── ProcessBettingAction/
    │   │   ├── ProcessBettingActionCommand.cs
    │   │   ├── ProcessBettingActionCommandHandler.cs
    │   │   ├── ProcessBettingActionEndpoint.cs
    │   │   └── ProcessBettingActionSuccessful.cs
    │   ├── ProcessBuyCard/          ← NEW: Baseball-specific
    │   │   ├── ProcessBuyCardCommand.cs
    │   │   ├── ProcessBuyCardCommandHandler.cs
    │   │   ├── ProcessBuyCardEndpoint.cs
    │   │   ├── ProcessBuyCardSuccessful.cs
    │   │   └── ProcessBuyCardError.cs
    │   └── PerformShowdown/
    │       ├── PerformShowdownCommand.cs
    │       ├── PerformShowdownCommandHandler.cs
    │       ├── PerformShowdownEndpoint.cs
    │       └── PerformShowdownSuccessful.cs
    └── Queries/
        └── GetCurrentPlayerTurn/
            ├── GetCurrentPlayerTurnQuery.cs
            ├── GetCurrentPlayerTurnQueryHandler.cs
            ├── GetCurrentPlayerTurnEndpoint.cs
            └── GetCurrentPlayerTurnResponse.cs
```

#### 4.3.1 BaseballApiMapGroup.cs

```csharp
using CardGames.Poker.Api.Features.Games.Baseball.v1;

namespace CardGames.Poker.Api.Features.Games.Baseball;

[EndpointMapGroup]
public static class BaseballApiMapGroup
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var baseball = app.NewVersionedApi("Baseball");
        baseball.MapV1();
    }
}
```

#### 4.3.2 v1/Feature.cs

```csharp
namespace CardGames.Poker.Api.Features.Games.Baseball.v1;

public static class Feature
{
    public const string Name = "Baseball";
}
```

#### 4.3.3 v1/V1.cs

```csharp
using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1;

public static class V1
{
    public static void MapV1(this IVersionedEndpointRouteBuilder app)
    {
        var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/baseball")
            .HasApiVersion(1.0)
            .WithTags([Feature.Name])
            .AddFluentValidationAutoValidation();

        mapGroup
            .MapStartHand()
            .MapCollectAntes()
            .MapDealHands()
            .MapProcessBettingAction()
            .MapProcessBuyCard()      // Baseball-specific endpoint
            .MapPerformShowdown()
            .MapGetCurrentPlayerTurn();
    }
}
```

---

### 4.4 ProcessBuyCard Command (Baseball-Specific)

This is the unique endpoint for Baseball's buy-card mechanic.

#### 4.4.1 ProcessBuyCardCommand.cs

```csharp
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

/// <summary>
/// Command to process a buy-card decision in Baseball poker.
/// </summary>
/// <param name="GameId">The game identifier.</param>
/// <param name="PlayerId">The player making the decision.</param>
/// <param name="Accept">True to buy the extra card, false to decline.</param>
public record ProcessBuyCardCommand(
    Guid GameId, 
    Guid PlayerId, 
    bool Accept
) : IRequest<OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>>, IGameStateChangingCommand;
```

#### 4.4.2 ProcessBuyCardCommandHandler.cs

```csharp
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public sealed class ProcessBuyCardCommandHandler 
    : IRequestHandler<ProcessBuyCardCommand, OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>>
{
    private readonly CardsDbContext _context;
    private readonly ILogger<ProcessBuyCardCommandHandler> _logger;

    public ProcessBuyCardCommandHandler(CardsDbContext context, ILogger<ProcessBuyCardCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>> Handle(
        ProcessBuyCardCommand request, 
        CancellationToken cancellationToken)
    {
        var game = await _context.Games
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Cards)
            .Include(g => g.Pots)
            .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

        if (game is null)
        {
            return new ProcessBuyCardError("Game not found");
        }

        if (!string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessBuyCardError("Buy card is only available in Baseball");
        }

        // Verify we're in a phase where buy-card is valid
        if (game.CurrentPhase != nameof(Phases.BuyCardOffer))
        {
            return new ProcessBuyCardError($"Buy card not available in phase {game.CurrentPhase}");
        }

        var player = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == request.PlayerId);
        if (player is null)
        {
            return new ProcessBuyCardError("Player not found in game");
        }

        // Check if this player has a pending buy-card offer
        if (!player.HasPendingBuyCardOffer)
        {
            return new ProcessBuyCardError("No pending buy card offer for this player");
        }

        var now = DateTimeOffset.UtcNow;
        Card? extraCard = null;

        if (request.Accept)
        {
            var buyCardPrice = game.BuyCardPrice ?? 0;
            
            if (player.ChipStack < buyCardPrice)
            {
                return new ProcessBuyCardError($"Insufficient chips. Need {buyCardPrice}, have {player.ChipStack}");
            }

            // Deduct chips (goes to pot, NOT a bet)
            player.ChipStack -= buyCardPrice;
            
            // Add to main pot
            var mainPot = game.Pots.FirstOrDefault(p => p.PotNumber == 0);
            if (mainPot is not null)
            {
                mainPot.Amount += buyCardPrice;
            }

            // Deal extra hole card from deck
            var deckCards = await _context.GameCards
                .Where(c => c.GameId == game.Id && 
                           c.HandNumber == game.CurrentHandNumber &&
                           c.Location == CardLocation.Deck &&
                           !c.IsDiscarded)
                .OrderBy(c => c.DealOrder)
                .ToListAsync(cancellationToken);

            if (deckCards.Count == 0)
            {
                return new ProcessBuyCardError("No cards remaining in deck");
            }

            var cardToDeal = deckCards.First();
            cardToDeal.GamePlayerId = player.Id;
            cardToDeal.Location = CardLocation.Hole;
            cardToDeal.IsVisible = false;
            cardToDeal.DealtAt = now;
            cardToDeal.DealtAtPhase = "BuyCard";

            extraCard = new Card((Suit)cardToDeal.Suit, (Symbol)cardToDeal.Symbol);

            _logger.LogInformation(
                "Player {PlayerName} bought extra card {Card} for {Price} chips in game {GameId}",
                player.Player.Name, extraCard, buyCardPrice, game.Id);
        }
        else
        {
            _logger.LogInformation(
                "Player {PlayerName} declined buy card offer in game {GameId}",
                player.Player.Name, game.Id);
        }

        // Clear the pending offer
        player.HasPendingBuyCardOffer = false;
        player.PendingBuyCardFourIndex = null;

        // Check if there are more buy-card offers pending for other players
        var morePendingOffers = game.GamePlayers.Any(gp => gp.HasPendingBuyCardOffer);

        if (!morePendingOffers)
        {
            // Return to the street phase
            game.CurrentPhase = game.PreviousPhase ?? nameof(Phases.ThirdStreet);
            game.PreviousPhase = null;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ProcessBuyCardSuccessful
        {
            Purchased = request.Accept,
            AmountPaid = request.Accept ? (game.BuyCardPrice ?? 0) : 0,
            ExtraCard = extraCard is not null 
                ? new CardDto(extraCard.Suit.ToString(), extraCard.Symbol.ToString()) 
                : null,
            MoreOffersRemaining = morePendingOffers
        };
    }
}
```

#### 4.4.3 ProcessBuyCardSuccessful.cs

```csharp
namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public record ProcessBuyCardSuccessful
{
    public bool Purchased { get; init; }
    public int AmountPaid { get; init; }
    public CardDto? ExtraCard { get; init; }
    public bool MoreOffersRemaining { get; init; }
}

public record CardDto(string Suit, string Symbol);
```

#### 4.4.4 ProcessBuyCardError.cs

```csharp
namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public record ProcessBuyCardError(string Message);
```

#### 4.4.5 ProcessBuyCardEndpoint.cs

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public static class ProcessBuyCardEndpoint
{
    public static RouteGroupBuilder MapProcessBuyCard(this RouteGroupBuilder group)
    {
        group.MapPost("/{gameId:guid}/buy-card", HandleAsync)
            .WithName("BaseballProcessBuyCard")
            .WithSummary("Process Buy Card Decision")
            .WithDescription("""
                Processes a player's decision to buy or decline an extra card when dealt a 4 face-up.
                
                **Buy Card Rules:**
                - When a 4 is dealt face-up, the player may pay the buy-card price for an extra hole card
                - The payment goes directly to the pot (it is NOT a bet)
                - The extra card is always dealt face-down
                - If declined, play continues normally
                """)
            .Produces<ProcessBuyCardSuccessful>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid gameId,
        [FromBody] ProcessBuyCardRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ProcessBuyCardCommand(gameId, request.PlayerId, request.Accept);
        var result = await mediator.Send(command, cancellationToken);

        return result.Match<IResult>(
            success => Results.Ok(success),
            error => Results.Conflict(new ProblemDetails
            {
                Title = "Buy Card Failed",
                Detail = error.Message,
                Status = StatusCodes.Status409Conflict
            }));
    }
}

public record ProcessBuyCardRequest(Guid PlayerId, bool Accept);
```

---

### 4.5 Database Entity Updates

**File:** `CardGames.Poker.Api/Data/Entities/GamePlayer.cs`

Add fields to track buy-card state:

```csharp
// Add to GamePlayer entity
/// <summary>
/// Indicates if this player has a pending buy-card offer (Baseball).
/// </summary>
public bool HasPendingBuyCardOffer { get; set; }

/// <summary>
/// The index of the board card (4) that triggered the buy-card offer.
/// </summary>
public int? PendingBuyCardFourIndex { get; set; }
```

**File:** `CardGames.Poker.Api/Data/Entities/Game.cs`

Add fields for game-level buy-card configuration:

```csharp
// Add to Game entity
/// <summary>
/// The price to buy an extra card when dealt a 4 face-up (Baseball).
/// </summary>
public int? BuyCardPrice { get; set; }

/// <summary>
/// Stores the previous phase when transitioning to BuyCardOffer.
/// </summary>
public string? PreviousPhase { get; set; }
```

**Migration:** Create a new migration for these schema changes.

---

### 4.6 TableStateBuilder Updates

**File:** `CardGames.Poker.Api/Services/TableStateBuilder.cs`

Add Baseball-specific hand evaluation in `BuildPrivateStateAsync` and `BuildShowdownPublicDtoAsync`:

```csharp
// In BuildPrivateStateAsync, add after SevenCardStud handling (around line 355):
else if (string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase))
{
    // Baseball uses same structure as Seven Card Stud but with wild cards
    var holeCards = playerCards.Where(c => c.Location == CardLocation.Hole)
        .OrderBy(c => c.DealOrder)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();
    var boardCards = playerCards.Where(c => c.Location == CardLocation.Board)
        .OrderBy(c => c.DealOrder)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();
    
    // Initial 2 hole cards, then any additional from buy-card
    var initialHole = holeCards.Take(2).ToList();
    var downCards = holeCards.Skip(2).ToList();
    
    var baseballHand = new BaseballHand(initialHole, boardCards, downCards);
    handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(baseballHand);
    
    // Include wild card info
    if (baseballHand.WildCards.Count > 0)
    {
        var wildCardNames = string.Join(", ", baseballHand.WildCards.Select(c => $"{c.Symbol} of {c.Suit}"));
        handEvaluationDescription += $" (Wild: {wildCardNames})";
    }
}

// In BuildShowdownPublicDtoAsync, add Baseball handling:
else if (string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.BaseballCode, StringComparison.OrdinalIgnoreCase))
{
    var holeCards = showdownCards.Where(c => c.Location == CardLocation.Hole)
        .OrderBy(c => c.DealOrder)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();
    var boardCards = showdownCards.Where(c => c.Location == CardLocation.Board)
        .OrderBy(c => c.DealOrder)
        .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
        .ToList();
    
    var initialHole = holeCards.Take(2).ToList();
    var downCards = holeCards.Skip(2).ToList();
    
    var baseballHand = new BaseballHand(initialHole, boardCards, downCards);
    handDescription = HandDescriptionFormatter.GetHandDescription(baseballHand);
    handStrength = baseballHand.Strength;
}
```

---

### 4.7 ContinuousPlayBackgroundService Updates

**File:** `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

Add handling for `BuyCardOffer` phase in the phase processing:

```csharp
// Add to inProgressPhases list (around line 118):
"BuyCardOffer",

// Add a new method for processing buy-card offers:
private async Task ProcessBuyCardOffersAsync(
    CardsDbContext context,
    IGameStateBroadcaster broadcaster,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Find games in BuyCardOffer phase where all pending offers have been processed
    var gamesInBuyCardPhase = await context.Games
        .Where(g => g.CurrentPhase == nameof(Phases.BuyCardOffer))
        .Include(g => g.GamePlayers)
        .Include(g => g.GameType)
        .ToListAsync(cancellationToken);

    foreach (var game in gamesInBuyCardPhase)
    {
        // Check if any players still have pending offers
        var pendingOffers = game.GamePlayers.Any(gp => gp.HasPendingBuyCardOffer);
        
        if (!pendingOffers)
        {
            // All offers processed, return to street phase
            game.CurrentPhase = game.PreviousPhase ?? nameof(Phases.ThirdStreet);
            game.PreviousPhase = null;
            
            await context.SaveChangesAsync(cancellationToken);
            await broadcaster.BroadcastTableStateAsync(game.Id, cancellationToken);
        }
    }
}
```

---

### 4.8 Phases Enum Update

**File:** `CardGames.Poker/Betting/Phases.cs`

Add the `BuyCardOffer` phase:

```csharp
// Add to Phases enum:
/// <summary>
/// Baseball: Player is being offered to buy an extra card after receiving a 4 face-up.
/// </summary>
BuyCardOffer,
```

---

## 5. Contracts Layer Implementation

### 5.1 Add Refit Interface

After implementing the API endpoints, regenerate the Refit interfaces by running:

```bash
cd src/CardGames.Poker.Refitter
dotnet run
```

This will auto-generate `IBaseballApi` interface in `CardGames.Contracts/RefitInterface.v1.cs`.

The generated interface should include:
- `BaseballStartHandAsync`
- `BaseballCollectAntesAsync`
- `BaseballDealHandsAsync`
- `BaseballProcessBettingActionAsync`
- `BaseballProcessBuyCardAsync` ← New endpoint
- `BaseballPerformShowdownAsync`
- `BaseballGetCurrentPlayerTurnAsync`

---

## 6. Blazor UI Implementation

### 6.1 Create API Client Wrapper

**File:** `CardGames.Poker.Web/Services/GameApi/BaseballApiClientWrapper.cs` (NEW)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class BaseballApiClientWrapper(IBaseballApi client) : IGameApiClient
{
    public string GameTypeCode => "BASEBALL";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        var startHandResponse = await client.BaseballStartHandAsync(gameId);
        if (!startHandResponse.IsSuccessStatusCode) return false;

        var collectAntesResponse = await client.BaseballCollectAntesAsync(gameId);
        if (!collectAntesResponse.IsSuccessStatusCode) return false;

        var dealHandsResponse = await client.BaseballDealHandsAsync(gameId);
        return dealHandsResponse.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(
        Guid gameId, ProcessBettingActionRequest request)
    {
        return await client.BaseballProcessBettingActionAsync(gameId, request);
    }

    public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        // Baseball has no draw phase
        return Task.FromResult(new ProcessDrawResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Draw phase not supported for Baseball." 
        });
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.BaseballPerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        // Baseball has no drop/stay phase
        return Task.FromResult(false);
    }

    // Baseball-specific method for buy-card
    public async Task<ProcessBuyCardResult> ProcessBuyCardAsync(Guid gameId, Guid playerId, bool accept)
    {
        var response = await client.BaseballProcessBuyCardAsync(gameId, new ProcessBuyCardRequest(playerId, accept));
        
        if (response.IsSuccessStatusCode && response.Content is not null)
        {
            return new ProcessBuyCardResult
            {
                IsSuccess = true,
                Purchased = response.Content.Purchased,
                AmountPaid = response.Content.AmountPaid
            };
        }
        
        return new ProcessBuyCardResult
        {
            IsSuccess = false,
            ErrorMessage = response.Error?.Content ?? "Buy card failed"
        };
    }
}

public class ProcessBuyCardResult
{
    public bool IsSuccess { get; set; }
    public bool Purchased { get; set; }
    public int AmountPaid { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 6.2 Update IGameApiClient Interface

**File:** `CardGames.Poker.Web/Services/GameApi/IGameApiClient.cs`

Add buy-card method (with default implementation for non-Baseball games):

```csharp
// Add to interface:
Task<ProcessBuyCardResult> ProcessBuyCardAsync(Guid gameId, Guid playerId, bool accept)
{
    return Task.FromResult(new ProcessBuyCardResult 
    { 
        IsSuccess = false, 
        ErrorMessage = "Buy card not supported for this game type" 
    });
}
```

### 6.3 Register API Client

**File:** `CardGames.Poker.Web/Program.cs`

Add registration for Baseball client:

```csharp
// Add after line 130 (after TwosJacksManWithTheAxeApiClientWrapper):
builder.Services.AddScoped<IGameApiClient, BaseballApiClientWrapper>();
```

Also register the Refit interface:

```csharp
// Add with other Refit registrations:
builder.Services
    .AddRefitClient<IBaseballApi>(
        settingsAction: _ => new RefitSettings(),
        httpClientName: "baseballApi")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://api"))
    .AddHttpMessageHandler<AuthenticationStateHandler>();
```

### 6.4 Create BuyCardOverlay Component

**File:** `CardGames.Poker.Web/Components/Shared/BuyCardOverlay.razor` (NEW)

```razor
@namespace CardGames.Poker.Web.Components.Shared

<div class="buy-card-overlay">
    <div class="overlay-content">
        <div class="overlay-header">
            <i class="fa-solid fa-baseball"></i>
            <h2>Buy Card Offer</h2>
        </div>
        
        <div class="buy-card-info">
            <p>You were dealt a <strong>4</strong> face-up!</p>
            <p>You may pay <strong>@BuyCardPrice chips</strong> to receive an extra face-down card.</p>
            
            <div class="four-card-display">
                <CardDisplay Card="@FourCard" />
            </div>
            
            <div class="chip-info">
                <span>Your chips: @PlayerChips</span>
                <span>Cost: @BuyCardPrice</span>
            </div>
        </div>
        
        <div class="buy-card-actions">
            <button class="btn btn-success btn-lg" 
                    @onclick="() => OnDecision.InvokeAsync(true)"
                    disabled="@(IsProcessing || PlayerChips < BuyCardPrice)">
                <i class="fa-solid fa-check"></i>
                Buy Card (@BuyCardPrice)
            </button>
            
            <button class="btn btn-secondary btn-lg" 
                    @onclick="() => OnDecision.InvokeAsync(false)"
                    disabled="@IsProcessing">
                <i class="fa-solid fa-xmark"></i>
                Decline
            </button>
        </div>
        
        @if (IsProcessing)
        {
            <div class="processing-indicator">
                <i class="fa-solid fa-spinner fa-spin"></i>
                Processing...
            </div>
        }
    </div>
</div>

@code {
    [Parameter] public CardDto? FourCard { get; set; }
    [Parameter] public int BuyCardPrice { get; set; }
    [Parameter] public int PlayerChips { get; set; }
    [Parameter] public bool IsProcessing { get; set; }
    [Parameter] public EventCallback<bool> OnDecision { get; set; }
}
```

**File:** `CardGames.Poker.Web/Components/Shared/BuyCardOverlay.razor.css` (NEW)

```css
.buy-card-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.85);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.overlay-content {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    border: 2px solid #d4af37;
    border-radius: 16px;
    padding: 2rem;
    max-width: 400px;
    text-align: center;
    box-shadow: 0 0 30px rgba(212, 175, 55, 0.3);
}

.overlay-header {
    margin-bottom: 1.5rem;
}

.overlay-header i {
    font-size: 2.5rem;
    color: #d4af37;
    margin-bottom: 0.5rem;
}

.overlay-header h2 {
    color: #fff;
    margin: 0;
}

.buy-card-info {
    color: #ccc;
    margin-bottom: 1.5rem;
}

.buy-card-info p {
    margin: 0.5rem 0;
}

.four-card-display {
    margin: 1rem 0;
}

.chip-info {
    display: flex;
    justify-content: space-between;
    background: rgba(0, 0, 0, 0.3);
    padding: 0.75rem 1rem;
    border-radius: 8px;
    margin-top: 1rem;
}

.buy-card-actions {
    display: flex;
    gap: 1rem;
    justify-content: center;
}

.buy-card-actions button {
    min-width: 120px;
}

.processing-indicator {
    margin-top: 1rem;
    color: #d4af37;
}
```

### 6.5 Update TablePlay.razor

**File:** `CardGames.Poker.Web/Components/Pages/TablePlay.razor`

Add Baseball-specific code:

```razor
@* Add inject for Baseball API (around line 28): *@
@inject IBaseballApi BaseballApi

@* Add helper property (around line 513): *@
private bool IsBaseball => string.Equals(_gameTypeCode, "BASEBALL", StringComparison.OrdinalIgnoreCase);

@* Add phase detection (around line 544): *@
private bool IsBuyCardOfferPhase => 
    string.Equals(_gameResponse?.CurrentPhase, "BuyCardOffer", StringComparison.OrdinalIgnoreCase);

@* Add state variables (around line 450): *@
private bool _hasPendingBuyCardOffer;
private int _buyCardPrice;
private bool _isProcessingBuyCard;

@* Add overlay rendering (around line 227, after Drop or Stay Overlay): *@
<!-- Buy Card Overlay (for Baseball) -->
@if (IsBaseball && IsBuyCardOfferPhase && _hasPendingBuyCardOffer && IsParticipatingInHand)
{
    <BuyCardOverlay 
        FourCard="@GetPendingFourCard()"
        BuyCardPrice="@_buyCardPrice"
        PlayerChips="@_playerChips"
        IsProcessing="@_isProcessingBuyCard"
        OnDecision="@HandleBuyCardDecision" />
}

@* Add handler method (in @code block): *@
private async Task HandleBuyCardDecision(bool accept)
{
    if (_currentPlayerId is null) return;
    
    _isProcessingBuyCard = true;
    await InvokeAsync(StateHasChanged);
    
    try
    {
        var client = GetBaseballClient();
        if (client is BaseballApiClientWrapper baseballClient)
        {
            var result = await baseballClient.ProcessBuyCardAsync(GameId, _currentPlayerId.Value, accept);
            
            if (result.IsSuccess)
            {
                _hasPendingBuyCardOffer = false;
                
                if (result.Purchased)
                {
                    await ShowToastAsync($"Bought extra card for {result.AmountPaid} chips!", "success");
                }
                else
                {
                    await ShowToastAsync("Declined buy card offer", "info");
                }
            }
            else
            {
                await ShowToastAsync(result.ErrorMessage ?? "Buy card failed", "error");
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error processing buy card decision");
        await ShowToastAsync("Error processing buy card", "error");
    }
    finally
    {
        _isProcessingBuyCard = false;
        await InvokeAsync(StateHasChanged);
    }
}

private CardDto? GetPendingFourCard()
{
    // Get the 4 card that triggered the buy offer from table state
    return _tableState?.BuyCardOffer?.FourCard;
}

private IGameApiClient GetBaseballClient()
{
    return _clients.FirstOrDefault(c => c.GameTypeCode == "BASEBALL") 
           ?? throw new InvalidOperationException("Baseball client not found");
}
```

### 6.6 Update SignalR DTOs

**File:** `CardGames.Contracts/SignalR/TableStatePublicDto.cs`

Add buy-card state:

```csharp
// Add to TableStatePublicDto:
/// <summary>
/// Current buy-card offer state (Baseball only).
/// </summary>
public BuyCardOfferDto? BuyCardOffer { get; set; }

// Add new DTO:
public record BuyCardOfferDto
{
    public Guid PlayerId { get; init; }
    public string PlayerName { get; init; }
    public CardDto FourCard { get; init; }
    public int BuyCardPrice { get; init; }
}
```

---

## 7. Testing Requirements

### 7.1 Unit Tests

Create tests in `Tests/CardGames.Poker.Tests/Games/Baseball/`:

| Test File | Coverage |
|-----------|----------|
| `BaseballGameTests.cs` | Game flow, dealing, buy-card logic |
| `BaseballHandTests.cs` | Wild card evaluation |
| `BaseballShowdownTests.cs` | Winner determination with wild cards |

### 7.2 Integration Tests

Create tests in `Tests/CardGames.IntegrationTests/`:

| Test File | Coverage |
|-----------|----------|
| `BaseballApiTests.cs` | Full game flow through API |
| `BaseballBuyCardTests.cs` | Buy-card endpoint scenarios |

### 7.3 Test Scenarios

#### Buy-Card Scenarios:
1. Player dealt 4 on Third Street, accepts buy → receives extra hole card
2. Player dealt 4 on Third Street, declines → continues normally
3. Player dealt 4 but insufficient chips → error returned
4. Player dealt 4 on Seventh Street → no offer (down card)
5. Multiple players dealt 4s → sequential offers processed

#### Wild Card Scenarios:
1. Hand with one 3 → wild card applied
2. Hand with one 9 → wild card applied
3. Hand with multiple wild cards → optimal evaluation
4. No wild cards → standard evaluation

---

## 8. Database Seeding

Add Baseball to the game type seed data.

**File:** `CardGames.Poker.Api/Data/CardsDbContext.cs` (or seed file)

```csharp
// Add to game type seed:
new GameType
{
    Id = Guid.Parse("00000000-0000-0000-0000-000000000008"), // Next available ID
    Code = "BASEBALL",
    Name = "Baseball",
    Description = "A seven-card stud variant with wild 3s and 9s, and buy-card options on 4s.",
    MinPlayers = 2,
    MaxPlayers = 6,
    InitialHoleCards = 2,
    InitialBoardCards = 1,
    MaxCommunityCards = 0,
    MaxPlayerCards = 10, // Can be more than 7 with buy cards
    HasDrawPhase = false,
    MaxDiscards = 0,
    WildCardRule = "ThreesAndNines",
    BettingStructure = "AnteBringIn",
    ImageName = "baseball.png"
}
```

---

## 9. Implementation Order

### Phase 1: Core API (P1)
1. Add `BaseballCode` constant to `PokerGameMetadataRegistry`
2. Add `BuyCardOffer` to `Phases` enum
3. Add database entity fields (`HasPendingBuyCardOffer`, `BuyCardPrice`, `PreviousPhase`)
4. Create database migration
5. Create `BaseballFlowHandler`
6. Create Baseball API feature folder with all endpoints
7. Update `TableStateBuilder` for Baseball hand evaluation
8. Update `ContinuousPlayBackgroundService` for buy-card phase

### Phase 2: Contracts & UI (P2)
1. Regenerate Refit interfaces
2. Create `BaseballApiClientWrapper`
3. Register Baseball client in DI
4. Create `BuyCardOverlay` component
5. Update `TablePlay.razor` with Baseball support

### Phase 3: Testing & Polish (P3)
1. Create unit tests
2. Create integration tests
3. Add Baseball image asset (`baseball.png`)
4. Update database seed data
5. End-to-end testing

---

## 10. Acceptance Criteria

### 10.1 Functional Requirements
- [ ] Players can create and join Baseball games
- [ ] Antes are collected correctly
- [ ] Cards are dealt in seven-card stud pattern
- [ ] When dealt a 4 face-up, player receives buy-card offer
- [ ] Buy-card payment goes to pot (not a bet)
- [ ] Extra cards from buy-card are dealt face-down
- [ ] 3s and 9s are evaluated as wild cards
- [ ] Showdown correctly evaluates wild card hands
- [ ] Best 5-card hand wins from all available cards

### 10.2 UI Requirements
- [ ] Baseball appears in game type selection
- [ ] Buy-card overlay appears when appropriate
- [ ] Wild cards are visually indicated
- [ ] Hand evaluation shows wild card substitutions
- [ ] Continuous play works correctly

### 10.3 Non-Functional Requirements
- [ ] API endpoints follow existing patterns
- [ ] Code passes all existing tests
- [ ] No regressions in other game types
- [ ] Performance: buy-card decision < 500ms response time

---

## 11. Glossary

| Term | Definition |
|------|------------|
| **Buy-Card** | The unique Baseball mechanic where receiving a 4 face-up allows paying for an extra hole card |
| **Wild Card** | 3s and 9s that can represent any card for hand evaluation |
| **Street** | A dealing/betting round in stud games (Third through Seventh Street) |
| **Bring-In** | The forced opening bet on Third Street by the player with the lowest visible card |
| **Board Cards** | Face-up cards visible to all players |
| **Hole Cards** | Face-down cards visible only to the player |

---

## 12. References

- `CardGames.Poker/Games/Baseball/BaseballGame.cs` - Domain implementation
- `CardGames.Poker/Hands/StudHands/BaseballHand.cs` - Hand evaluation
- `CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs` - Similar flow handler pattern
- `docs/GameTypes.md` - Extensibility analysis
- `docs/GameTypes3.md` - Code change points
