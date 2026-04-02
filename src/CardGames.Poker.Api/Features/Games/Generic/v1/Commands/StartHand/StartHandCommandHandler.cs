using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.InMemoryEngine;
using CardGames.Poker.Betting;
using CardGames.Contracts.SignalR;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Generic handler for starting a new hand in any poker variant.
/// Uses <see cref="IGameFlowHandlerFactory"/> to route to game-specific logic.
/// </summary>
/// <remarks>
/// <para>
/// This handler replaces the duplicated StartHandCommandHandler implementations
/// across game-specific feature folders (FiveCardDraw, SevenCardStud, KingsAndLows, etc.)
/// by extracting common operations and delegating game-specific behavior to flow handlers.
/// </para>
/// <para>
/// Common operations performed by this handler:
/// <list type="bullet">
///   <item><description>Load game with players</description></item>
///   <item><description>Validate game phase (WaitingToStart or Complete)</description></item>
///   <item><description>Process pending leave requests</description></item>
///   <item><description>Apply pending chips</description></item>
///   <item><description>Auto-sit-out players with insufficient chips</description></item>
///   <item><description>Get eligible players</description></item>
///   <item><description>Reset player states</description></item>
///   <item><description>Remove previous hand's cards</description></item>
///   <item><description>Create new main pot</description></item>
/// </list>
/// </para>
/// <para>
/// Game-specific operations delegated to <see cref="IGameFlowHandler"/>:
/// <list type="bullet">
///   <item><description>Determine initial phase (via <see cref="IGameFlowHandler.GetInitialPhase"/>)</description></item>
///   <item><description>Game-specific initialization (via <see cref="IGameFlowHandler.OnHandStartingAsync"/>)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class StartHandCommandHandler(
    CardsDbContext context,
    IGameFlowHandlerFactory flowHandlerFactory,
    IGameStateBroadcaster broadcaster,
    IOptions<InMemoryEngineOptions> engineOptions,
    IGameStateManager gameStateManager,
    ILogger<StartHandCommandHandler> logger)
    : IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
    /// <inheritdoc />
    public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
        StartHandCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Load the game with its players and game type
        var game = await context.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new StartHandError
            {
                Message = $"Game with ID '{command.GameId}' was not found.",
                Code = StartHandErrorCode.GameNotFound
            };
        }

        // 2. Dealer's Choice first-hand: set initial DC dealer and wait for game type selection
        if (game.IsDealersChoice && game.GameTypeId is null)
        {
            if (game.CurrentPhase != nameof(Phases.WaitingToStart))
            {
                return new StartHandError
                {
                    Message = $"Cannot start a Dealer's Choice game from '{game.CurrentPhase}' phase.",
                    Code = StartHandErrorCode.InvalidGameState
                };
            }

            // Set initial DC dealer to the same as the game dealer (seat 0 or first occupied seat)
            var firstActiveSeat = game.GamePlayers
                .Where(gp => gp.Status == GamePlayerStatus.Active)
                .OrderBy(gp => gp.SeatPosition)
                .Select(gp => gp.SeatPosition)
                .FirstOrDefault();

            game.DealersChoiceDealerPosition = firstActiveSeat;
            game.DealerPosition = firstActiveSeat;
            game.CurrentHandNumber = 1;
            game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
            game.Status = GameStatus.InProgress;
            game.StartedAt ??= now;
            game.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);

            if (engineOptions.Value.Enabled)
                await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);

            logger.LogInformation(
                "Dealer's Choice game {GameId}: waiting for dealer at seat {DcDealerSeat} to choose game type",
                game.Id, game.DealersChoiceDealerPosition);

            return new StartHandSuccessful
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                CurrentPhase = game.CurrentPhase,
                ActivePlayerCount = game.GamePlayers.Count(gp => gp.Status == GamePlayerStatus.Active)
            };
        }

        // 3. Get the game flow handler for this game type
        var gameTypeCode = game.GameType?.Code
            ?? throw new InvalidOperationException(
                $"Game {game.Id} has no GameType assigned. Cannot determine flow handler.");
        if (!flowHandlerFactory.TryGetHandler(gameTypeCode, out var flowHandler) || flowHandler is null)
        {
            logger.LogWarning(
                "No flow handler found for game type {GameType}, using default",
                gameTypeCode);
            flowHandler = flowHandlerFactory.GetHandler(gameTypeCode);
        }

        logger.LogInformation(
            "Starting hand for game {GameId} using {GameType} flow handler",
            game.Id, flowHandler.GameTypeCode);

        // 4. Validate game state allows starting a new hand
        var validPhases = new[]
        {
            nameof(Phases.WaitingToStart),
            nameof(Phases.Complete)
        };

        if (!validPhases.Contains(game.CurrentPhase))
        {
            return new StartHandError
            {
                Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase. " +
                          $"A new hand can only be started when the game is in '{nameof(Phases.WaitingToStart)}' " +
                          $"or '{nameof(Phases.Complete)}' phase.",
                Code = StartHandErrorCode.InvalidGameState
            };
        }

        // 4. Finalize leave requests for players who were waiting for the hand to finish
        ProcessPendingLeaveRequests(game, now);

        // 5. Apply pending chips to player stacks
        ApplyPendingChips(game);

        // 6. Auto-sit-out players with insufficient chips for the ante
        AutoSitOutPlayersWithInsufficientChips(game);

        // 7. Get eligible players (active, not sitting out, chips >= ante or ante is 0)
        var eligiblePlayers = GetEligiblePlayers(game);

        if (eligiblePlayers.Count < 2)
        {
            return new StartHandError
            {
                Message = "Not enough eligible players to start a new hand. At least 2 players with sufficient chips are required.",
                Code = StartHandErrorCode.NotEnoughPlayers
            };
        }

        // 8. Reset player states for new hand
        ResetPlayerStates(game);

        // 9. Prepare card state for the upcoming hand.
        await flowHandler.PrepareForNewHandAsync(
            context,
            game,
            eligiblePlayers,
            game.CurrentHandNumber + 1,
            now,
            cancellationToken);

        // 10. Game-specific initialization (e.g., reset DropOrStay decisions for Kings and Lows)
        await flowHandler.OnHandStartingAsync(game, cancellationToken);

        // 11. Create a new main pot for this hand
        var mainPot = new Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber + 1,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = 0,
            IsAwarded = false,
            CreatedAt = now
        };

        context.Pots.Add(mainPot);

        // 12. Update game state
        game.CurrentHandNumber++;
        game.CurrentPhase = flowHandler.GetInitialPhase(game);
        game.Status = GameStatus.InProgress;
        game.CurrentPlayerIndex = -1;
        game.CurrentDrawPlayerIndex = -1;
        game.HandCompletedAt = null;
        game.NextHandStartsAt = null;
        game.UpdatedAt = now;

        // Set StartedAt only on first hand
        game.StartedAt ??= now;

        // 13. Persist changes
        await context.SaveChangesAsync(cancellationToken);

        // Blind-based / skip-ante variants should auto-deal immediately after the initial hand state is saved.
        // Ante-based variants intentionally wait in CollectingAntes until antes are posted.
        if (flowHandler.SkipsAnteCollection)
        {
            await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);

            if (await ShouldBroadcastScrewYourNeighborNewDeckToastAsync(context, flowHandler, game, cancellationToken))
            {
                await broadcaster.BroadcastTableToastAsync(
                    new TableToastNotificationDto
                    {
                        GameId = game.Id,
                        Message = "Starting new deck"
                    },
                    cancellationToken);
            }
        }
        else if (flowHandler.AutoCollectsAntesOnStart)
        {
            // Pot-funded games (e.g., In-Between) collect antes and deal in one step
            // because they have no separate CollectAntes endpoint.
            var ante = game.Ante ?? 0;
            if (ante > 0)
            {
                foreach (var player in eligiblePlayers)
                {
                    var anteAmount = Math.Min(ante, player.ChipStack);
                    player.ChipStack -= anteAmount;
                    player.CurrentBet = anteAmount;
                    player.TotalContributedThisHand = anteAmount;

                    mainPot.Amount += anteAmount;

                    context.Set<PotContribution>().Add(new PotContribution
                    {
                        PotId = mainPot.Id,
                        GamePlayerId = player.Id,
                        Amount = anteAmount,
                        ContributedAt = now
                    });

                    if (player.ChipStack == 0)
                    {
                        player.IsAllIn = true;
                    }
                }
            }

            await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (engineOptions.Value.Enabled)
            await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);

        logger.LogInformation(
            "Started hand {HandNumber} for game {GameId} in phase {Phase} with {PlayerCount} players",
            game.CurrentHandNumber, game.Id, game.CurrentPhase, eligiblePlayers.Count);

        return new StartHandSuccessful
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            CurrentPhase = game.CurrentPhase,
            ActivePlayerCount = eligiblePlayers.Count
        };
    }

    private static async Task<bool> ShouldBroadcastScrewYourNeighborNewDeckToastAsync(
        CardsDbContext context,
        IGameFlowHandler flowHandler,
        Game game,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(flowHandler.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase) ||
            game.CurrentHandNumber <= 1)
        {
            return false;
        }

        var currentHandCardCount = await context.GameCards
            .CountAsync(gc => gc.GameId == game.Id && gc.HandNumber == game.CurrentHandNumber, cancellationToken);

        return currentHandCardCount == 52;
    }

    /// <summary>
    /// Processes pending leave requests for players who were waiting for the hand to finish.
    /// </summary>
    private static void ProcessPendingLeaveRequests(Game game, DateTimeOffset now)
    {
        var playersLeaving = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
            .ToList();

        foreach (var player in playersLeaving)
        {
            player.Status = GamePlayerStatus.Left;
            player.LeftAt = now;
            player.FinalChipCount = player.ChipStack;
            player.IsSittingOut = true;
        }
    }

    /// <summary>
    /// Applies pending chips to player stacks.
    /// </summary>
    private static void ApplyPendingChips(Game game)
    {
        var playersWithPendingChips = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
            .ToList();

        foreach (var player in playersWithPendingChips)
        {
            player.ChipStack += player.PendingChipsToAdd;
            player.PendingChipsToAdd = 0;
        }
    }

    /// <summary>
    /// Auto-sits-out players with insufficient chips for the ante.
    /// </summary>
    private static void AutoSitOutPlayersWithInsufficientChips(Game game)
    {
        var ante = game.Ante ?? 0;
        var playersWithInsufficientChips = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         gp.ChipStack < ante)
            .ToList();

        foreach (var player in playersWithInsufficientChips)
        {
            player.IsSittingOut = true;
            player.Status = GamePlayerStatus.SittingOut;
        }
    }

    /// <summary>
    /// Gets eligible players (active, not sitting out, chips >= ante or ante is 0).
    /// </summary>
    private static List<GamePlayer> GetEligiblePlayers(Game game)
    {
        var ante = game.Ante ?? 0;
        return game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         (ante == 0 || gp.ChipStack >= ante))
            .DistinctBy(gp => gp.Id)
            .ToList();
    }

    /// <summary>
    /// Resets player states for a new hand.
    /// </summary>
    private static void ResetPlayerStates(Game game)
    {
        foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
        {
            gamePlayer.CurrentBet = 0;
            gamePlayer.TotalContributedThisHand = 0;
            gamePlayer.IsAllIn = false;
            gamePlayer.HasDrawnThisRound = false;
            gamePlayer.HasFolded = gamePlayer.IsSittingOut;
            gamePlayer.VariantState = null;
        }
    }
}
