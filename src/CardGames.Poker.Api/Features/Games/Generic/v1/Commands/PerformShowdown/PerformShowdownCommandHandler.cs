using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using System.Text.Json;
using CardSuit = CardGames.Poker.Api.Data.Entities.CardSuit;
using CardSymbol = CardGames.Poker.Api.Data.Entities.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;

/// <summary>
/// Generic handler for performing showdown in any poker variant.
/// Uses <see cref="IGameFlowHandlerFactory"/> for game-specific phase transitions
/// and <see cref="IHandEvaluatorFactory"/> for game-specific hand evaluation.
/// </summary>
/// <remarks>
/// <para>
/// This handler replaces the duplicated PerformShowdownCommandHandler implementations
/// across game-specific feature folders by extracting common operations and delegating
/// game-specific behavior to evaluators and flow handlers.
/// </para>
/// <para>
/// Common operations performed by this handler:
/// <list type="bullet">
///   <item><description>Load game with players, cards, and pots</description></item>
///   <item><description>Validate game is in showdown phase</description></item>
///   <item><description>Handle win-by-fold scenarios</description></item>
///   <item><description>Evaluate hands using game-specific evaluator</description></item>
///   <item><description>Award pots (including side pots)</description></item>
///   <item><description>Update player chip stacks</description></item>
///   <item><description>Move dealer button</description></item>
///   <item><description>Record hand history</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PerformShowdownCommandHandler(
    CardsDbContext context,
    IGameFlowHandlerFactory flowHandlerFactory,
    IHandEvaluatorFactory handEvaluatorFactory,
    IHandHistoryRecorder handHistoryRecorder,
    ILogger<PerformShowdownCommandHandler> logger)
    : IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
    /// <inheritdoc />
    public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
        PerformShowdownCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Load the game with its players, cards, and pots (including contributions for eligibility)
        var game = await context.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GameType)
            .Include(g => g.Pots)
                .ThenInclude(p => p.Contributions)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new PerformShowdownError
            {
                Message = $"Game with ID '{command.GameId}' was not found.",
                Code = PerformShowdownErrorCode.GameNotFound
            };
        }

        // 2. Get the game flow handler and hand evaluator
        var gameTypeCode = game.GameType?.Code ?? "FIVECARDDRAW";
        var flowHandler = flowHandlerFactory.GetHandler(gameTypeCode);
        var handEvaluator = handEvaluatorFactory.GetEvaluator(gameTypeCode);

        logger.LogInformation(
            "Performing showdown for game {GameId} using {GameType} evaluator",
            game.Id, gameTypeCode);

        // Filter pots to current hand
        var currentHandPots = game.Pots.Where(p => p.HandNumber == game.CurrentHandNumber).ToList();
        var isAlreadyAwarded = currentHandPots.Any(p => p.IsAwarded);

        // 3. Validate game is in showdown phase or pots are already awarded
        if (game.CurrentPhase != nameof(Phases.Showdown) && !isAlreadyAwarded)
        {
            return new PerformShowdownError
            {
                Message = $"Cannot perform showdown. Game is in '{game.CurrentPhase}' phase. " +
                          $"Showdown can only be performed when the game is in '{nameof(Phases.Showdown)}' phase.",
                Code = PerformShowdownErrorCode.InvalidGameState
            };
        }

        // 4. Get players who have not folded
        var playersInHand = game.GamePlayers
            .Where(gp => !gp.HasFolded && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn))
            .ToList();

        // Fetch user first names from Users table
        var playerEmails = game.GamePlayers
            .Where(gp => gp.Player.Email != null)
            .Select(gp => gp.Player.Email!)
            .ToList();

        var usersByEmail = await context.Users
            .AsNoTracking()
            .Where(u => u.Email != null && playerEmails.Contains(u.Email))
            .Select(u => new UserInfo(u.Email!, u.FirstName))
            .ToDictionaryAsync(u => u.Email, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // 5. Load cards for players in hand
        var playerCards = await context.GameCards
            .Where(c => c.GameId == command.GameId &&
                        c.HandNumber == game.CurrentHandNumber &&
                        !c.IsDiscarded &&
                        c.GamePlayerId != null &&
                        playersInHand.Select(p => p.Id).Contains(c.GamePlayerId.Value))
            .ToListAsync(cancellationToken);

        var playerCardGroups = playerCards
            .GroupBy(c => c.GamePlayerId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 6. Calculate total pot
        var totalPot = currentHandPots.Sum(p => p.Amount);

        // 7. Handle win by fold (only one player remaining)
        if (playersInHand.Count == 1)
        {
            return await HandleWinByFoldAsync(
                game, playersInHand[0], currentHandPots, totalPot, 
                playerCardGroups, usersByEmail, isAlreadyAwarded, now, cancellationToken);
        }

        // 8. Handle no players remaining (dead hand - everyone dropped)
        if (playersInHand.Count == 0)
        {
            return await HandleDeadHandAsync(
                game, currentHandPots, totalPot, flowHandler,
                isAlreadyAwarded, now, cancellationToken);
        }

        // 9. Evaluate all hands using the game-specific evaluator
        var playerHandEvaluations = EvaluatePlayerHands(playersInHand, playerCardGroups, handEvaluator);

        // 10. Award each pot to the best hand among ELIGIBLE players
        var (payouts, allWinners, overallWinReason) = await AwardPotsAsync(
            game, currentHandPots, playerHandEvaluations, isAlreadyAwarded, now);

        if (!isAlreadyAwarded)
        {
            // 11. Update player chip stacks
            foreach (var payout in payouts)
            {
                var gamePlayer = playerHandEvaluations[payout.Key].GamePlayer;
                gamePlayer.ChipStack += payout.Value;
            }

            // 12. Update game state - use flow handler to determine next phase
            var nextPhase = flowHandler.GetNextPhase(game, nameof(Phases.Showdown)) 
                ?? nameof(Phases.Complete);
            
            game.CurrentPhase = nextPhase;
            game.UpdatedAt = now;

            if (nextPhase == nameof(Phases.Complete))
            {
                game.HandCompletedAt = now;
                game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
                UpdateSitOutStatus(game);
                MoveDealer(game);
                await flowHandler.OnHandCompletedAsync(game, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            // 13. Record hand history
            await RecordHandHistoryAsync(
                game,
                game.GamePlayers.ToList(),
                playerCardGroups,
                now,
                totalPot,
                wonByFold: false,
                winners: allWinners.Select(w => (
                    playerHandEvaluations[w].GamePlayer.PlayerId,
                    w,
                    payouts[w]
                )).ToList(),
                winnerNames: allWinners.ToList(),
                winningHandDescription: overallWinReason,
                cancellationToken);
        }

        // 14. Build response
        var playerHandsList = BuildPlayerHandsResponse(
            playerHandEvaluations, allWinners, payouts, usersByEmail, handEvaluator);

        return new PerformShowdownSuccessful
        {
            GameId = game.Id,
            WonByFold = false,
            CurrentPhase = game.CurrentPhase,
            Payouts = payouts,
            PlayerHands = playerHandsList
        };
    }

    /// <summary>
    /// Handles a win-by-fold scenario where only one player remains.
    /// </summary>
    private async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> HandleWinByFoldAsync(
        Game game,
        GamePlayer winner,
        List<Data.Entities.Pot> currentHandPots,
        int totalPot,
        Dictionary<Guid, List<GameCard>> playerCardGroups,
        Dictionary<string, UserInfo> usersByEmail,
        bool isAlreadyAwarded,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!isAlreadyAwarded)
        {
            winner.ChipStack += totalPot;

            // Mark pots as awarded
            foreach (var pot in currentHandPots)
            {
                pot.IsAwarded = true;
                pot.AwardedAt = now;
                pot.WinReason = "All others folded";

                var winnerPayoutsList = new[] { new { playerId = winner.PlayerId.ToString(), playerName = winner.Player.Name, amount = totalPot } };
                pot.WinnerPayouts = JsonSerializer.Serialize(winnerPayoutsList);
            }

            game.CurrentPhase = nameof(Phases.Complete);
            game.UpdatedAt = now;
            game.HandCompletedAt = now;
            game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
            UpdateSitOutStatus(game);
            MoveDealer(game);

            await context.SaveChangesAsync(cancellationToken);

            // Record hand history for win-by-fold
            await RecordHandHistoryAsync(
                game,
                game.GamePlayers.ToList(),
                playerCardGroups,
                now,
                totalPot,
                wonByFold: true,
                winners: [(winner.PlayerId, winner.Player.Name, totalPot)],
                winnerNames: [winner.Player.Name],
                winningHandDescription: null,
                cancellationToken);
        }

        var winnerCards = playerCardGroups.GetValueOrDefault(winner.Id, []);
        usersByEmail.TryGetValue(winner.Player.Email ?? string.Empty, out var winnerUser);

        return new PerformShowdownSuccessful
        {
            GameId = game.Id,
            WonByFold = true,
            CurrentPhase = game.CurrentPhase,
            Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
            PlayerHands =
            [
                new ShowdownPlayerHand
                {
                    PlayerName = winner.Player.Name,
                    PlayerFirstName = winnerUser?.FirstName,
                    Cards = winnerCards.Select(c => new ShowdownCard
                    {
                        Suit = c.Suit,
                        Symbol = c.Symbol
                    }).ToList(),
                    HandType = null,
                    HandStrength = null,
                    IsWinner = true,
                    AmountWon = totalPot
                }
            ]
        };
    }

    /// <summary>
    /// Handles a dead hand scenario where no players remain (everyone dropped).
    /// </summary>
    private async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> HandleDeadHandAsync(
        Game game,
        List<Data.Entities.Pot> currentHandPots,
        int totalPot,
        IGameFlowHandler flowHandler,
        bool isAlreadyAwarded,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!isAlreadyAwarded)
        {
            // Mark current pots as awarded (no winner - pot carries forward for some games)
            foreach (var pot in currentHandPots)
            {
                pot.IsAwarded = true;
                pot.AwardedAt = now;
                pot.WinReason = "Everyone dropped - pot carries forward";
            }

            // Check if this game type carries pots forward
            if (flowHandler.SpecialPhases.Contains(nameof(Phases.DropOrStay)))
            {
                // Create new pot for next hand with carried pot
                var nextHandPot = new Data.Entities.Pot
                {
                    GameId = game.Id,
                    HandNumber = game.CurrentHandNumber + 1,
                    PotType = PotType.Main,
                    PotOrder = 0,
                    Amount = totalPot,
                    IsAwarded = false,
                    CreatedAt = now
                };
                context.Pots.Add(nextHandPot);
            }

            game.CurrentPhase = nameof(Phases.Complete);
            game.UpdatedAt = now;
            game.HandCompletedAt = now;
            game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
            MoveDealer(game);

            await context.SaveChangesAsync(cancellationToken);
        }

        return new PerformShowdownSuccessful
        {
            GameId = game.Id,
            WonByFold = false,
            CurrentPhase = game.CurrentPhase,
            Payouts = new Dictionary<string, int>(),
            PlayerHands = []
        };
    }

    /// <summary>
    /// Evaluates player hands using the game-specific evaluator.
    /// </summary>
    private static Dictionary<string, PlayerHandEvaluation> EvaluatePlayerHands(
        List<GamePlayer> playersInHand,
        Dictionary<Guid, List<GameCard>> playerCardGroups,
        IHandEvaluator handEvaluator)
    {
        var evaluations = new Dictionary<string, PlayerHandEvaluation>();

        foreach (var gamePlayer in playersInHand)
        {
            if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards) || cards.Count < 5)
            {
                continue; // Skip players without valid hands
            }

            HandBase hand;
            if (handEvaluator.SupportsPositionalCards)
            {
                // For stud-style games, separate hole and board cards
                var holeCards = cards
                    .Where(c => c.Location == CardLocation.Hole)
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                var boardCards = cards
                    .Where(c => c.Location == CardLocation.Board)
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                var downCards = cards
                    .Where(c => c.Location == CardLocation.Hole)
                    .OrderBy(c => c.DealOrder)
                    .Skip(2) // First 2 are initial hole cards
                    .Select(c => MapToCard(c))
                    .ToList();

                hand = handEvaluator.CreateHand(holeCards.Take(2).ToList(), boardCards, downCards);
            }
            else
            {
                // For draw-style games, just pass all cards
                var coreCards = cards
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                hand = handEvaluator.CreateHand(coreCards);
            }

            evaluations[gamePlayer.Player.Name] = new PlayerHandEvaluation(hand, cards, gamePlayer);
        }

        return evaluations;
    }

    /// <summary>
    /// Awards pots to winners based on hand strength and eligibility.
    /// </summary>
    private async Task<(Dictionary<string, int> Payouts, HashSet<string> AllWinners, string? OverallWinReason)> AwardPotsAsync(
        Game game,
        List<Data.Entities.Pot> currentHandPots,
        Dictionary<string, PlayerHandEvaluation> playerHandEvaluations,
        bool isAlreadyAwarded,
        DateTimeOffset now)
    {
        var payouts = new Dictionary<string, int>();
        var allWinners = new HashSet<string>();
        string? overallWinReason = null;

        if (!isAlreadyAwarded)
        {
            // Order pots by PotOrder (main pot first, then side pots)
            var orderedPots = currentHandPots.OrderBy(p => p.PotOrder).ToList();

            foreach (var pot in orderedPots)
            {
                if (pot.Amount == 0)
                {
                    continue;
                }

                // Get eligible players for this pot from contributions
                var eligiblePlayerIds = pot.Contributions
                    .Where(c => c.IsEligibleToWin)
                    .Select(c => c.GamePlayerId)
                    .ToHashSet();

                // If no contribution records exist, fall back to all players in hand
                if (eligiblePlayerIds.Count == 0)
                {
                    eligiblePlayerIds = playerHandEvaluations.Values
                        .Select(e => e.GamePlayer.Id)
                        .ToHashSet();
                }

                // Filter hand evaluations to only eligible players
                var eligibleHands = playerHandEvaluations
                    .Where(kvp => eligiblePlayerIds.Contains(kvp.Value.GamePlayer.Id))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (eligibleHands.Count == 0)
                {
                    continue;
                }

                // Determine winner(s) for this pot
                var maxStrength = eligibleHands.Values.Max(h => h.Hand.Strength);
                var potWinners = eligibleHands
                    .Where(kvp => kvp.Value.Hand.Strength == maxStrength)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // Calculate payouts for this pot
                var potPayoutPerWinner = pot.Amount / potWinners.Count;
                var potRemainder = pot.Amount % potWinners.Count;

                var potPayoutsList = new List<object>();
                foreach (var winner in potWinners)
                {
                    var payout = potPayoutPerWinner;
                    if (potRemainder > 0)
                    {
                        payout++;
                        potRemainder--;
                    }

                    // Add to total payouts
                    if (payouts.TryGetValue(winner, out var existingPayout))
                    {
                        payouts[winner] = existingPayout + payout;
                    }
                    else
                    {
                        payouts[winner] = payout;
                    }

                    allWinners.Add(winner);

                    var gp = eligibleHands[winner].GamePlayer;
                    potPayoutsList.Add(new { playerId = gp.PlayerId.ToString(), playerName = winner, amount = payout });
                }

                // Mark pot as awarded
                var winReason = potWinners.Count > 1
                    ? $"Split pot - {eligibleHands[potWinners[0]].Hand.Type}"
                    : eligibleHands[potWinners[0]].Hand.Type.ToString();

                pot.IsAwarded = true;
                pot.AwardedAt = now;
                pot.WinReason = winReason;
                pot.WinnerPayouts = JsonSerializer.Serialize(potPayoutsList);

                // Track overall win reason (use main pot's reason)
                if (pot.PotOrder == 0)
                {
                    overallWinReason = winReason;
                }
            }
        }
        else
        {
            // Pots already awarded - reconstruct payouts from stored data
            foreach (var pot in currentHandPots.Where(p => p.WinnerPayouts != null))
            {
                try
                {
                    var potPayouts = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(pot.WinnerPayouts!);
                    if (potPayouts != null)
                    {
                        foreach (var potPayout in potPayouts)
                        {
                            if (potPayout.TryGetValue("playerName", out var nameObj) &&
                                potPayout.TryGetValue("amount", out var amountObj))
                            {
                                var name = nameObj.ToString()!;
                                var amount = Convert.ToInt32(((JsonElement)amountObj).GetInt32());
                                if (payouts.TryGetValue(name, out var existing))
                                {
                                    payouts[name] = existing + amount;
                                }
                                else
                                {
                                    payouts[name] = amount;
                                }
                                allWinners.Add(name);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors for legacy data
                }
            }
        }

        return (payouts, allWinners, overallWinReason);
    }

    /// <summary>
    /// Builds the player hands response for the showdown result.
    /// </summary>
    private static List<ShowdownPlayerHand> BuildPlayerHandsResponse(
        Dictionary<string, PlayerHandEvaluation> playerHandEvaluations,
        HashSet<string> allWinners,
        Dictionary<string, int> payouts,
        Dictionary<string, UserInfo> usersByEmail,
        IHandEvaluator handEvaluator)
    {
        return playerHandEvaluations.Select(kvp =>
        {
            var isWinner = allWinners.Contains(kvp.Key);
            usersByEmail.TryGetValue(kvp.Value.GamePlayer.Player.Email ?? string.Empty, out var user);

            // Get the best evaluated cards (handles wild card substitution)
            var bestCards = handEvaluator.GetEvaluatedBestCards(kvp.Value.Hand);

            return new ShowdownPlayerHand
            {
                PlayerName = kvp.Key,
                PlayerFirstName = user?.FirstName,
                Cards = kvp.Value.Cards.Select(c => new ShowdownCard
                {
                    Suit = c.Suit,
                    Symbol = c.Symbol
                }).ToList(),
                HandType = kvp.Value.Hand.Type.ToString(),
                HandStrength = kvp.Value.Hand.Strength,
                IsWinner = isWinner,
                AmountWon = payouts.GetValueOrDefault(kvp.Key, 0)
            };
        }).OrderByDescending(h => h.HandStrength ?? 0).ToList();
    }

    /// <summary>
    /// Records the hand history for completed hands.
    /// </summary>
    private async Task RecordHandHistoryAsync(
        Game game,
        List<GamePlayer> allPlayers,
        Dictionary<Guid, List<GameCard>> playerCardGroups,
        DateTimeOffset completedAt,
        int totalPot,
        bool wonByFold,
        List<(Guid PlayerId, string PlayerName, int AmountWon)> winners,
        List<string> winnerNames,
        string? winningHandDescription,
        CancellationToken cancellationToken)
    {
        var winnerNameSet = winnerNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isSplitPot = winners.Count > 1;

        var playerResults = allPlayers.Select(gp =>
        {
            var isWinner = winnerNameSet.Contains(gp.Player.Name);
            var netDelta = isWinner
                ? winners.First(w => w.PlayerId == gp.PlayerId).AmountWon - gp.TotalContributedThisHand
                : -gp.TotalContributedThisHand;

            List<string>? showdownCards = null;
            var reachedShowdown = !gp.HasFolded && !wonByFold;
            if (reachedShowdown && playerCardGroups.TryGetValue(gp.Id, out var cards) && cards.Count != 0)
            {
                showdownCards = cards
                    .OrderBy(c => c.DealOrder)
                    .Select(c => FormatCard(c.Symbol, c.Suit))
                    .ToList();
            }

            return new PlayerResultInfo
            {
                PlayerId = gp.PlayerId,
                PlayerName = gp.Player.Name,
                SeatPosition = gp.SeatPosition,
                HasFolded = gp.HasFolded,
                ReachedShowdown = reachedShowdown,
                IsWinner = isWinner,
                IsSplitPot = isSplitPot,
                NetChipDelta = netDelta,
                WentAllIn = gp.IsAllIn,
                ShowdownCards = showdownCards
            };
        }).ToList();

        var winnerInfos = winners.Select(w => new Services.WinnerInfo
        {
            PlayerId = w.PlayerId,
            PlayerName = w.PlayerName,
            AmountWon = w.AmountWon
        }).ToList();

        await handHistoryRecorder.RecordHandHistoryAsync(
            new RecordHandHistoryParameters
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                CompletedAtUtc = completedAt,
                WonByFold = wonByFold,
                TotalPot = totalPot,
                WinningHandDescription = winningHandDescription,
                Winners = winnerInfos,
                PlayerResults = playerResults
            },
            cancellationToken);
    }

    /// <summary>
    /// Updates the status of players with zero chips to sitting out.
    /// </summary>
    private static void UpdateSitOutStatus(Game game)
    {
        foreach (var player in game.GamePlayers)
        {
            if (player.ChipStack <= 0 && player.Status == GamePlayerStatus.Active)
            {
                player.IsSittingOut = true;
                player.Status = GamePlayerStatus.SittingOut;
            }
        }
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

        if (seatsAfterCurrent.Count > 0)
        {
            game.DealerPosition = seatsAfterCurrent.First();
        }
        else
        {
            game.DealerPosition = occupiedSeats.First();
        }
    }

    /// <summary>
    /// Maps a GameCard entity to a Card domain object.
    /// </summary>
    private static Card MapToCard(GameCard gameCard) =>
        new(MapSuit(gameCard.Suit), MapSymbol(gameCard.Symbol));

    /// <summary>
    /// Maps entity CardSuit to core library Suit.
    /// </summary>
    private static Suit MapSuit(CardSuit suit) => suit switch
    {
        CardSuit.Hearts => Suit.Hearts,
        CardSuit.Diamonds => Suit.Diamonds,
        CardSuit.Spades => Suit.Spades,
        CardSuit.Clubs => Suit.Clubs,
        _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
    };

    /// <summary>
    /// Maps entity CardSymbol to core library Symbol.
    /// </summary>
    private static Symbol MapSymbol(CardSymbol symbol) => symbol switch
    {
        CardSymbol.Deuce => Symbol.Deuce,
        CardSymbol.Three => Symbol.Three,
        CardSymbol.Four => Symbol.Four,
        CardSymbol.Five => Symbol.Five,
        CardSymbol.Six => Symbol.Six,
        CardSymbol.Seven => Symbol.Seven,
        CardSymbol.Eight => Symbol.Eight,
        CardSymbol.Nine => Symbol.Nine,
        CardSymbol.Ten => Symbol.Ten,
        CardSymbol.Jack => Symbol.Jack,
        CardSymbol.Queen => Symbol.Queen,
        CardSymbol.King => Symbol.King,
        CardSymbol.Ace => Symbol.Ace,
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
    };

    /// <summary>
    /// Formats a card for display in hand history.
    /// </summary>
    private static string FormatCard(CardSymbol symbol, CardSuit suit)
    {
        var symbolStr = symbol switch
        {
            CardSymbol.Ten => "10",
            CardSymbol.Jack => "J",
            CardSymbol.Queen => "Q",
            CardSymbol.King => "K",
            CardSymbol.Ace => "A",
            _ => ((int)symbol + 2).ToString()
        };

        var suitStr = suit switch
        {
            CardSuit.Hearts => "h",
            CardSuit.Diamonds => "d",
            CardSuit.Clubs => "c",
            CardSuit.Spades => "s",
            _ => "?"
        };

        return $"{symbolStr}{suitStr}";
    }

    /// <summary>
    /// Holds evaluation results for a player's hand.
    /// </summary>
    private sealed record PlayerHandEvaluation(HandBase Hand, List<GameCard> Cards, GamePlayer GamePlayer);

    /// <summary>
    /// Holds user information for display purposes.
    /// </summary>
    private sealed record UserInfo(string Email, string? FirstName);
}
