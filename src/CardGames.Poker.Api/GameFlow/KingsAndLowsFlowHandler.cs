using System.Text.Json;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Hands.DrawHands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using DropOrStayDecision = CardGames.Poker.Api.Data.Entities.DropOrStayDecision;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Kings and Lows poker.
/// </summary>
/// <remarks>
/// <para>
/// Kings and Lows is a unique poker variant with a non-linear flow:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Deal 5 cards face-down to each player</description></item>
///   <item><description>Drop or Stay decision (players choose to fold or continue)</description></item>
///   <item><description>Based on remaining players:
///     <list type="bullet">
///       <item><description>0 players stay → Complete</description></item>
///       <item><description>1 player stays → Player vs Deck</description></item>
///       <item><description>2+ players stay → Draw Phase</description></item>
///     </list>
///   </description></item>
///   <item><description>Draw Phase → Draw Complete → Showdown</description></item>
///   <item><description>Showdown → Complete (losers automatically match the pot)</description></item>
/// </list>
/// <para>
/// Wild cards: All Kings and each player's lowest card are wild.
/// </para>
/// </remarks>
public sealed class KingsAndLowsFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "KINGSANDLOWS";

    /// <inheritdoc />
    public override GameRules GetGameRules() => KingsAndLowsRules.CreateGameRules();

    /// <inheritdoc />
    public override string GetInitialPhase(Game game)
    {
        // Kings and Lows: Deal first, then players decide to drop or stay.
        // Antes are only collected on the first hand; subsequent hands get pot from losers matching.
        // The background service handles the pot carryover, so we skip collecting antes.
        return nameof(Phases.Dealing);
    }

    /// <inheritdoc />
    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Kings and Lows has a unique non-linear flow based on game state
        // Pot matching happens automatically in PerformShowdownAsync, so no PotMatching phase needed
        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.Dealing),
            nameof(Phases.Dealing) => nameof(Phases.DropOrStay),
            nameof(Phases.DropOrStay) => DeterminePostDropPhase(game),
            nameof(Phases.DrawPhase) => nameof(Phases.DrawComplete),
            nameof(Phases.DrawComplete) => nameof(Phases.Showdown),
            nameof(Phases.PlayerVsDeck) => nameof(Phases.Complete),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    /// <inheritdoc />
    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.AllAtOnce,
            InitialCardsPerPlayer = 5,
            AllFaceDown = true
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Kings and Lows collects antes only on the very first hand. Subsequent hands
    /// get their pot from losers matching the previous pot. The background service
    /// handles pot carryover, so we skip the standard ante collection phase.
    /// </remarks>
    public override bool SkipsAnteCollection => true;

    /// <inheritdoc />
    public override IReadOnlyList<string> SpecialPhases =>
        [nameof(Phases.DropOrStay), nameof(Phases.PlayerVsDeck)];

    /// <inheritdoc />
    public override Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Reset all players' DropOrStay decisions and pot matching status
        foreach (var player in game.GamePlayers)
        {
            player.DropOrStayDecision = DropOrStayDecision.Undecided;
        }

        return Task.CompletedTask;
    }

    #region Chip Check

    /// <inheritdoc />
    public override bool RequiresChipCoverageCheck => true;

    /// <inheritdoc />
    public override ChipCheckConfiguration GetChipCheckConfiguration() =>
        ChipCheckConfiguration.KingsAndLowsDefault;

    #endregion

    #region Showdown

    /// <inheritdoc />
    public override bool SupportsInlineShowdown => true;

    /// <inheritdoc />
    public override async Task<ShowdownResult> PerformShowdownAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

        // Find staying players
        var stayingPlayers = gamePlayersList
            .Where(gp => !gp.HasFolded &&
                         gp is { Status: GamePlayerStatus.Active, DropOrStayDecision: DropOrStayDecision.Stay })
            .ToList();

        // Load main pot
        var mainPot = await context.Pots
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                      p.HandNumber == game.CurrentHandNumber,
                             cancellationToken);

        if (mainPot == null)
        {
            return ShowdownResult.Failure("No pot to award");
        }

        // Evaluate hands
        var playerHandEvaluations = new List<(GamePlayer player, long strength)>();

        foreach (var player in stayingPlayers)
        {
            var playerCards = game.GameCards
                .Where(gc => gc.GamePlayerId == player.Id &&
                             gc.HandNumber == game.CurrentHandNumber &&
                             !gc.IsDiscarded)
                .OrderBy(gc => gc.DealOrder)
                .Select(gc => new Card(
                    (Suit)(int)gc.Suit,
                    (Symbol)(int)gc.Symbol))
                .ToList();

            if (playerCards.Count >= 5)
            {
                var hand = new KingsAndLowsDrawHand(playerCards);
                playerHandEvaluations.Add((player, hand.Strength));
            }
        }

        if (playerHandEvaluations.Count == 0)
        {
            return ShowdownResult.Failure("No valid hands to evaluate");
        }

        // Find winners
        var maxStrength = playerHandEvaluations.Max(h => h.strength);
        var winners = playerHandEvaluations
            .Where(h => h.strength == maxStrength)
            .Select(h => h.player)
            .ToList();
        var losers = stayingPlayers.Where(p => !winners.Contains(p)).ToList();

        // Distribute pot
        var potAmount = mainPot.Amount;
        var sharePerWinner = potAmount / winners.Count;
        var remainder = potAmount % winners.Count;
        var payouts = new List<(Guid playerId, string name, int amount)>();

        foreach (var winner in winners)
        {
            var payout = sharePerWinner;
            if (remainder > 0)
            {
                payout++;
                remainder--;
            }
            winner.ChipStack += payout;
            payouts.Add((winner.PlayerId, winner.Player?.Name ?? "Unknown", payout));
        }

        // Handle pot matching for losers automatically
        var totalMatched = 0;
        foreach (var loser in losers)
        {
            var matchAmount = Math.Min(potAmount, loser.ChipStack);

            if (matchAmount > 0)
            {
                loser.ChipStack -= matchAmount;
                // Update contribution so hand history reflects the loss
                loser.TotalContributedThisHand += matchAmount;
                totalMatched += matchAmount;
            }
        }

        // Create pot for next hand if there was any matching
        if (totalMatched > 0)
        {
            var nextHandPot = new Data.Entities.Pot
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber + 1,
                PotType = PotType.Main,
                PotOrder = 0,
                Amount = totalMatched,
                IsAwarded = false,
                CreatedAt = now
            };
            context.Pots.Add(nextHandPot);
        }

        // Mark pot as awarded
        mainPot.IsAwarded = true;
        mainPot.AwardedAt = now;
        mainPot.WinnerPayouts = JsonSerializer.Serialize(
            payouts.Select(p => new { playerId = p.playerId.ToString(), playerName = p.name, amount = p.amount }));

        // Set timestamps for continuous play - required for background service to start next hand
        game.HandCompletedAt = now;
        game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
        game.UpdatedAt = now;
        MoveDealer(game);

        await context.SaveChangesAsync(cancellationToken);

        // Build winning hand description
        string? winningHandDescription = null;
        if (winners.Count > 0)
        {
            var winnerPlayer = winners[0];
            var winnerCards = game.GameCards
                .Where(gc => gc.GamePlayerId == winnerPlayer.Id &&
                             gc.HandNumber == game.CurrentHandNumber &&
                             !gc.IsDiscarded)
                .OrderBy(gc => gc.DealOrder)
                .Select(gc => new Card((Suit)(int)gc.Suit, (Symbol)(int)gc.Symbol))
                .ToList();

            if (winnerCards.Count >= 5)
            {
                var winnerHand = new KingsAndLowsDrawHand(winnerCards);
                winningHandDescription = HandDescriptionFormatter.GetHandDescription(winnerHand);
            }
        }

        // Record hand history
        await RecordHandHistoryAsync(
            handHistoryRecorder, game, gamePlayersList, stayingPlayers,
            potAmount, winners, losers, winningHandDescription, payouts, now, cancellationToken);

        return ShowdownResult.Success(
            winners.Select(w => w.PlayerId).ToList(),
            losers.Select(l => l.PlayerId).ToList(),
            potAmount,
            winningHandDescription);
    }

    private static async Task RecordHandHistoryAsync(
        IHandHistoryRecorder handHistoryRecorder,
        Game game,
        List<GamePlayer> gamePlayersList,
        List<GamePlayer> stayingPlayers,
        int potAmount,
        List<GamePlayer> winners,
        List<GamePlayer> losers,
        string? winningHandDescription,
        List<(Guid playerId, string name, int amount)> payouts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var winnerInfos = winners.Select(w =>
        {
            var payout = payouts.FirstOrDefault(p => p.playerId == w.PlayerId);
            return new WinnerInfo
            {
                PlayerId = w.PlayerId,
                PlayerName = w.Player?.Name ?? w.PlayerId.ToString(),
                AmountWon = payout.amount
            };
        }).ToList();

        var isSplitPot = winners.Count > 1;
        var winnerPlayerIds = winners.Select(w => w.PlayerId).ToHashSet();

        var playerResults = gamePlayersList.Select(gp =>
        {
            var isWinner = winnerPlayerIds.Contains(gp.PlayerId);
            var payoutAmount = isWinner ? winnerInfos.FirstOrDefault(w => w.PlayerId == gp.PlayerId)?.AmountWon ?? 0 : 0;
            var netDelta = isWinner
                ? payoutAmount - gp.TotalContributedThisHand
                : -gp.TotalContributedThisHand;

            // Get cards for this player if they reached showdown
            List<string>? showdownCards = null;
            var reachedShowdown = !gp.HasFolded && stayingPlayers.Contains(gp);
            if (reachedShowdown)
            {
                var cards = game.GameCards
                    .Where(gc => gc.GamePlayerId == gp.Id && gc.HandNumber == game.CurrentHandNumber && !gc.IsDiscarded)
                    .OrderBy(gc => gc.DealOrder)
                    .Select(gc => FormatCard(gc.Symbol, gc.Suit))
                    .ToList();
                if (cards.Count > 0)
                {
                    showdownCards = cards;
                }
            }

            return new PlayerResultInfo
            {
                PlayerId = gp.PlayerId,
                PlayerName = gp.Player?.Name ?? gp.PlayerId.ToString(),
                SeatPosition = gp.SeatPosition,
                HasFolded = gp.HasFolded,
                ReachedShowdown = reachedShowdown,
                IsWinner = isWinner,
                IsSplitPot = isSplitPot && isWinner,
                NetChipDelta = netDelta,
                WentAllIn = gp.IsAllIn,
                FoldStreet = gp.HasFolded ? "DropOrStay" : null,
                ShowdownCards = showdownCards
            };
        }).ToList();

        await handHistoryRecorder.RecordHandHistoryAsync(new RecordHandHistoryParameters
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            CompletedAtUtc = now,
            WonByFold = false,
            TotalPot = potAmount,
            WinningHandDescription = winningHandDescription,
            Winners = winnerInfos,
            PlayerResults = playerResults
        }, cancellationToken);
    }

    private static string FormatCard(CardSymbol symbol, CardSuit suit)
    {
        var symbolStr = symbol switch
        {
            CardSymbol.Deuce => "2",
            CardSymbol.Three => "3",
            CardSymbol.Four => "4",
            CardSymbol.Five => "5",
            CardSymbol.Six => "6",
            CardSymbol.Seven => "7",
            CardSymbol.Eight => "8",
            CardSymbol.Nine => "9",
            CardSymbol.Ten => "10",
            CardSymbol.Jack => "J",
            CardSymbol.Queen => "Q",
            CardSymbol.King => "K",
            CardSymbol.Ace => "A",
            _ => "?"
        };

        var suitStr = suit switch
        {
            CardSuit.Hearts => "h",
            CardSuit.Diamonds => "d",
            CardSuit.Spades => "s",
            CardSuit.Clubs => "c",
            _ => "?"
        };

        return $"{symbolStr}{suitStr}";
    }

    #endregion

    #region Post-Phase Processing

    /// <inheritdoc />
    public override Task<string> ProcessDrawCompleteAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // After draw, go directly to Showdown (pot matching happens automatically in PerformShowdownAsync)
        return Task.FromResult(nameof(Phases.Showdown));
    }

    /// <inheritdoc />
    public override async Task PerformAutoActionAsync(AutoActionContext context)
    {
        if (context.CurrentPhase.Equals("DropOrStay", StringComparison.OrdinalIgnoreCase))
        {
            await PerformAutoDropOrStayAsync(context);
        }
        else
        {
            await base.PerformAutoActionAsync(context);
        }
    }

    protected override async Task SendDrawActionAsync(AutoActionContext context)
    {
        var player = context.Game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == context.PlayerSeatIndex);
        if (player is null)
        {
            // Try to load from DB if not in memory
            player = await context.DbContext.GamePlayers
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GameId == context.GameId && gp.SeatPosition == context.PlayerSeatIndex, context.CancellationToken);
        }

        if (player is null)
        {
            context.Logger.LogWarning("Player not found for auto-draw action in KingsAndLows game {GameId}, seat {SeatIndex}", context.GameId, context.PlayerSeatIndex);
            return;
        }

        var command = new DrawCardsCommand(context.GameId, player.PlayerId, []);
        try
        {
            await context.Mediator.Send(command, context.CancellationToken);
            context.Logger.LogInformation("Auto-stand-pat completed for Kings and Lows game {GameId}", context.GameId);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Auto-stand-pat failed for Kings and Lows game {GameId}", context.GameId);
        }
    }

    private async Task PerformAutoDropOrStayAsync(AutoActionContext context)
    {
        context.Logger.LogInformation("Performing auto-drop action for game {GameId}", context.GameId);

        if (context.PlayerSeatIndex == -1)
        {
            // Global timer - handle all undecided players
            var players = await context.DbContext.GamePlayers
                .Where(gp => gp.GameId == context.GameId &&
                             gp.Status == GamePlayerStatus.Active &&
                             !gp.HasFolded &&
                             (!gp.DropOrStayDecision.HasValue || gp.DropOrStayDecision == DropOrStayDecision.Undecided))
                .ToListAsync(context.CancellationToken);

            foreach (var player in players)
            {
                await SendDropCommandAsync(context, player.PlayerId);
            }
        }
        else
        {
            // Single player timer
            var player = context.Game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == context.PlayerSeatIndex);
             if (player is null)
            {
                player = await context.DbContext.GamePlayers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(gp => gp.GameId == context.GameId && gp.SeatPosition == context.PlayerSeatIndex, context.CancellationToken);
            }

             if (player != null)
             {
                 await SendDropCommandAsync(context, player.PlayerId);
             }
        }
    }

    private async Task SendDropCommandAsync(AutoActionContext context, Guid playerId)
    {
        try
        {
            var command = new DropOrStayCommand(context.GameId, playerId, "Drop");
            await context.Mediator.Send(command, context.CancellationToken);
            context.Logger.LogInformation("Auto-drop completed for player {PlayerId} in game {GameId}", playerId, context.GameId);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Auto-drop failed for game {GameId}", context.GameId);
        }
    }

    #endregion


    /// <summary>
    /// Determines the phase to transition to after the DropOrStay phase completes.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The next phase name based on the number of staying players.</returns>
    private static string DeterminePostDropPhase(Game game)
    {
        var stayingPlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.HasFolded &&
                         !gp.IsSittingOut &&
                         gp.DropOrStayDecision == DropOrStayDecision.Stay)
            .DistinctBy(gp => gp.PlayerId)
            .Count();

        return stayingPlayers switch
        {
            0 => nameof(Phases.Complete),
            1 => nameof(Phases.PlayerVsDeck),
            _ => nameof(Phases.DrawPhase)
        };
    }

    /// <summary>
    /// Moves the dealer button to the next occupied seat position (clockwise).
    /// </summary>
    private static void MoveDealer(Game game)
    {
        var occupiedSeats = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active)
            .OrderBy(gp => gp.SeatPosition)
            .Select(gp => gp.SeatPosition)
            .ToList();

        if (occupiedSeats.Count == 0)
        {
            return;
        }

        var currentPosition = game.DealerPosition;
        var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

        game.DealerPosition = seatsAfterCurrent.Count > 0
            ? seatsAfterCurrent.First()
            : occupiedSeats.First();
    }
}
