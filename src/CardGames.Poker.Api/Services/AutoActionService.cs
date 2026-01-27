using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Performs automatic actions when a player's turn timer expires.
/// </summary>
public sealed class AutoActionService : IAutoActionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoActionService> _logger;

    /// <summary>
    /// Phases where betting actions apply.
    /// </summary>
    private static readonly HashSet<string> BettingPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "FirstBettingRound",
        "SecondBettingRound"
    };

    /// <summary>
    /// Phases where draw actions apply.
    /// </summary>
    private static readonly HashSet<string> DrawPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "DrawPhase"
    };

    /// <summary>
    /// Phases where drop/stay decisions apply.
    /// </summary>
    private static readonly HashSet<string> DropOrStayPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "DropOrStay"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoActionService"/> class.
    /// </summary>
    public AutoActionService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoActionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PerformAutoActionAsync(Guid gameId, int playerSeatIndex, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Performing auto-action for game {GameId}, player seat {SeatIndex}",
            gameId, playerSeatIndex);

        // Create a new scope for this operation since we're called from a timer callback
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Get the game to determine the current phase and game type
        var game = await context.Games
            .Include(g => g.GameType)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found for auto-action", gameId);
            return;
        }

        var currentPhase = game.CurrentPhase;
        var gameTypeCode = game.GameType?.Code ?? "";

        // Handle simultaneous action timer expiration (seat index -1)
        if (playerSeatIndex == -1)
        {
            if (DropOrStayPhases.Contains(currentPhase))
            {
                await PerformAutoDropOrStayForUndecidedPlayersAsync(mediator, context, gameId, gameTypeCode, cancellationToken);
                return;
            }
            
            _logger.LogWarning("Global timer expired for phase {Phase} but no handler defined", currentPhase);
            return;
        }

        // Get the player at the specified seat
        var gamePlayer = await context.GamePlayers
            .Include(gp => gp.Player)
            .AsNoTracking()
            .FirstOrDefaultAsync(gp => gp.GameId == gameId && gp.SeatPosition == playerSeatIndex, cancellationToken);

        if (gamePlayer is null)
        {
            _logger.LogWarning(
                "No player found at seat {SeatIndex} in game {GameId} for auto-action",
                playerSeatIndex, gameId);
            return;
        }

        _logger.LogDebug(
            "Auto-action context: GameId={GameId}, Phase={Phase}, GameType={GameType}, PlayerId={PlayerId}",
            gameId, currentPhase, gameTypeCode, gamePlayer.PlayerId);

        // Route to the appropriate auto-action based on phase
        if (BettingPhases.Contains(currentPhase))
        {
            await PerformAutoBettingActionAsync(mediator, context, gameId, playerSeatIndex, gameTypeCode, cancellationToken);
        }
        else if (DrawPhases.Contains(currentPhase))
        {
            await PerformAutoDrawActionAsync(mediator, gameId, gamePlayer.PlayerId, gameTypeCode, cancellationToken);
        }
        else if (DropOrStayPhases.Contains(currentPhase))
        {
            await PerformAutoDropOrStayActionAsync(mediator, gameId, gamePlayer.PlayerId, gameTypeCode, cancellationToken);
        }
        else
        {
            _logger.LogDebug(
                "No auto-action defined for phase {Phase} in game {GameId}",
                currentPhase, gameId);
        }
    }

    private async Task PerformAutoDropOrStayForUndecidedPlayersAsync(
        IMediator mediator,
        CardsDbContext context,
        Guid gameId,
        string gameTypeCode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing auto-drop for all undecided players in game {GameId}", gameId);
        
        var players = await context.GamePlayers
            .Where(gp => gp.GameId == gameId && 
                         gp.Status == GamePlayerStatus.Active && 
                         !gp.HasFolded && 
                         (!gp.DropOrStayDecision.HasValue || gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Undecided))
            .ToListAsync(cancellationToken);
            
        foreach (var player in players)
        {
            await PerformAutoDropOrStayActionAsync(mediator, gameId, player.PlayerId, gameTypeCode, cancellationToken);
        }
    }

    /// <summary>
    /// Performs an automatic betting action (Check if possible, otherwise Fold).
    /// </summary>
    private async Task PerformAutoBettingActionAsync(
        IMediator mediator,
        CardsDbContext context,
        Guid gameId,
        int playerSeatIndex,
        string gameTypeCode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing auto-betting action for game {GameId}", gameId);

        // Determine if Check is available by comparing current bet to player's bet
        // Check is available when the player has already matched the current bet (no additional chips needed)
        var canCheck = await CanPlayerCheckAsync(context, gameId, playerSeatIndex, cancellationToken);

        var actionType = canCheck ? BettingActionType.Check : BettingActionType.Fold;
        _logger.LogInformation(
            "Auto-action determined for game {GameId}: {Action} (CanCheck={CanCheck})",
            gameId, actionType, canCheck);

        var command = new ProcessBettingActionCommand(gameId, actionType, 0);

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            result.Switch(
                success => _logger.LogInformation(
                    "Auto-{Action} completed for game {GameId}, round complete: {RoundComplete}",
                    actionType, gameId, success.RoundComplete),
                error => _logger.LogWarning(
                    "Auto-{Action} failed for game {GameId}: {Error}",
                    actionType, gameId, error.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing auto-betting action for game {GameId}", gameId);
        }
    }

    /// <summary>
    /// Determines if the player at the given seat can check (no bet to call).
    /// </summary>
    private async Task<bool> CanPlayerCheckAsync(
        CardsDbContext context,
        Guid gameId,
        int playerSeatIndex,
        CancellationToken cancellationToken)
    {
        // Get the game's current betting state
        var game = await context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            return false;
            }

            // Get the player's current bet
            var gamePlayer = await context.GamePlayers
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GameId == gameId && gp.SeatPosition == playerSeatIndex, cancellationToken);

            if (gamePlayer is null)
            {
                return false;
            }

            // Get the current bet amount in the round
            // The current bet is the maximum bet any player has made in this round
            var currentBetInRound = await context.GamePlayers
                .Where(gp => gp.GameId == gameId && gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
                .Select(gp => gp.CurrentBet)
                .MaxAsync(cancellationToken);

            // Player can check if their current bet equals the table's current bet
            var playerCurrentBet = gamePlayer.CurrentBet;
            var canCheck = currentBetInRound == playerCurrentBet;

            _logger.LogDebug(
                "CanCheck calculation for game {GameId}, seat {SeatIndex}: CurrentBetInRound={CurrentBet}, PlayerBet={PlayerBet}, CanCheck={CanCheck}",
                gameId, playerSeatIndex, currentBetInRound, playerCurrentBet, canCheck);

            return canCheck;
        }

        /// <summary>
    /// Performs an automatic draw action (Stand pat - keep all cards).
    /// </summary>
    private async Task PerformAutoDrawActionAsync(
        IMediator mediator,
        Guid gameId,
        Guid playerId,
        string gameTypeCode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing auto-draw action (stand pat) for game {GameId}", gameId);

        try
        {
            if (gameTypeCode.Equals("KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
            {
                var command = new DrawCardsCommand(gameId, playerId, []);
                var result = await mediator.Send(command, cancellationToken);
                result.Switch(
                    success => _logger.LogInformation(
                        "Auto-stand-pat completed for Kings and Lows game {GameId}",
                        gameId),
                    error => _logger.LogWarning(
                        "Auto-stand-pat failed for Kings and Lows game {GameId}: {Error}",
                        gameId, error.Message));
            }
            else
            {
                // Five Card Draw and other games
                var command = new ProcessDrawCommand(gameId, []);
                var result = await mediator.Send(command, cancellationToken);
                result.Switch(
                    success => _logger.LogInformation(
                        "Auto-stand-pat completed for game {GameId}, draw complete: {DrawComplete}",
                        gameId, success.DrawComplete),
                    error => _logger.LogWarning(
                        "Auto-stand-pat failed for game {GameId}: {Error}",
                        gameId, error.Message));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing auto-draw action for game {GameId}", gameId);
        }
    }

    /// <summary>
    /// Performs an automatic drop/stay action (Drop - fold out of the hand).
    /// </summary>
    private async Task PerformAutoDropOrStayActionAsync(
        IMediator mediator,
        Guid gameId,
        Guid playerId,
        string gameTypeCode,
        CancellationToken cancellationToken)
    {
                _logger.LogInformation("Performing auto-drop action for game {GameId}", gameId);

                if (!gameTypeCode.Equals("KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Drop/Stay phase only applies to Kings and Lows games");
                    return;
                }

                try
                {
                    var command = new DropOrStayCommand(gameId, playerId, "Drop");
                    var result = await mediator.Send(command, cancellationToken);
                    result.Switch(
                        success => _logger.LogInformation(
                            "Auto-drop completed for game {GameId}, all players decided: {Complete}",
                            gameId, success.AllPlayersDecided),
                        error => _logger.LogWarning(
                            "Auto-drop failed for game {GameId}: {Error}",
                            gameId, error.Message));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error performing auto-drop action for game {GameId}", gameId);
                }
            }
        }
