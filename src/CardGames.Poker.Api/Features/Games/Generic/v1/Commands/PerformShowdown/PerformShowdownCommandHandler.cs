using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.InMemoryEngine;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.StudHands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    IHandSettlementService handSettlementService,
    IOptions<InMemoryEngineOptions> engineOptions,
    IGameStateManager gameStateManager,
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

        // 2. Get the game flow handler and hand evaluator.
        // Dealer's Choice hands should evaluate/showdown against the selected hand type.
        var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code
            ?? throw new InvalidOperationException(
                $"Game {game.Id} has no GameType or CurrentHandGameTypeCode assigned. Cannot determine flow handler.");
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

        var playerEmails = game.GamePlayers
            .Where(gp => gp.Player.Email != null)
            .Select(gp => gp.Player.Email!)
            .ToList();

        var usersByEmail = await context.Users
            .AsNoTracking()
            .Where(u => u.Email != null && playerEmails.Contains(u.Email))
            .Select(u => new UserInfo(u.Email!, u.FirstName, u.LastName))
            .ToDictionaryAsync(u => u.Email, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Inline-showdown variants (for example Screw Your Neighbor) own their showdown
        // settlement logic and must not go through generic poker hand evaluation.
        if (flowHandler.SupportsInlineShowdown && !isAlreadyAwarded)
        {
            var showdownResult = await flowHandler.PerformShowdownAsync(
                context,
                game,
                handHistoryRecorder,
                now,
                cancellationToken);

            if (!showdownResult.IsSuccess)
            {
                return new PerformShowdownError
                {
                    Message = showdownResult.ErrorMessage ?? "Showdown failed.",
                    Code = PerformShowdownErrorCode.InvalidGameState
                };
            }

            var nextPhase = await flowHandler.ProcessPostShowdownAsync(
                context,
                game,
                showdownResult,
                now,
                cancellationToken);

            game.CurrentPhase = nextPhase;
            game.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);

            var inlineHandCards = await context.GameCards
                .Where(c => c.GameId == command.GameId &&
                            c.HandNumber == game.CurrentHandNumber &&
                            !c.IsDiscarded &&
                            c.GamePlayerId != null)
                .OrderBy(c => c.DealOrder)
                .ToListAsync(cancellationToken);

            var inlineCardsByGamePlayerId = inlineHandCards
                .Where(c => c.GamePlayerId.HasValue)
                .GroupBy(c => c.GamePlayerId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var inlinePayouts = new Dictionary<string, int>();
            var settlementPayouts = new Dictionary<string, int>();
            var inlineWinnerIdSet = showdownResult.WinnerPlayerIds.ToHashSet();
            var inlineParticipantPlayerIds = showdownResult.WinnerPlayerIds
                .Concat(showdownResult.LoserPlayerIds)
                .ToHashSet();

            if (ShouldSettleTerminalScrewYourNeighborVariant(game, showdownResult) &&
                showdownResult.WinnerPlayerIds.Count == 1 &&
                showdownResult.TotalPotAwarded > 0)
            {
                var winner = game.GamePlayers
                    .FirstOrDefault(gp => gp.PlayerId == showdownResult.WinnerPlayerIds[0]);
                if (winner is not null)
                {
                    // UI payout should show the amount won from the pot.
                    inlinePayouts[winner.Player.Name] = showdownResult.TotalPotAwarded;

                    // Settlement payout must include the winner's own hand contribution so
                    // netDelta = payout - contribution still credits the full pot amount.
                    settlementPayouts[winner.Player.Name] = showdownResult.TotalPotAwarded + winner.TotalContributedThisHand;
                }
            }

            // Load cards for each player so the overlay can display them
            var inlineCards = await context.GameCards
                .Where(c => c.GameId == game.Id &&
                             c.HandNumber == game.CurrentHandNumber &&
                             !c.IsDiscarded &&
                             c.GamePlayerId != null &&
                             c.Location == CardLocation.Hand)
                .ToListAsync(cancellationToken);

            var inlineCardsByPlayer = inlineCards
                .GroupBy(c => c.GamePlayerId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DealOrder).ToList());

            var inlinePlayerHands = playersInHand
                .Select(gamePlayer =>
                {
                    usersByEmail.TryGetValue(gamePlayer.Player.Email ?? string.Empty, out var user);

                    var canonicalPlayerName = gamePlayer.Player.Name;
                    var firstName = ResolvePlayerFirstName(user, canonicalPlayerName);

                    var cards = inlineCardsByPlayer.TryGetValue(gamePlayer.Id, out var playerCards)
                        ? playerCards.Select(c => new ShowdownCard { Suit = c.Suit, Symbol = c.Symbol }).ToList()
                        : new List<ShowdownCard>();

                    return new ShowdownPlayerHand
                    {
                        PlayerName = canonicalPlayerName,
                        PlayerFirstName = firstName,
                        Cards = cards,
                        HandType = null,
                        HandDescription = null,
                        HandStrength = null,
                        IsWinner = inlineWinnerIdSet.Contains(gamePlayer.PlayerId),
                        AmountWon = inlinePayouts.GetValueOrDefault(canonicalPlayerName, 0)
                    };
                })
                .ToList();

            // Inline-showdown variants still need cashier settlement updates.
            await handSettlementService.SettleHandAsync(game, settlementPayouts, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            if (engineOptions.Value.Enabled)
                await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);

            return new PerformShowdownSuccessful
            {
                GameId = game.Id,
                WonByFold = showdownResult.WonByFold,
                CurrentPhase = game.CurrentPhase,
                Payouts = inlinePayouts,
                PlayerHands = inlinePlayerHands
            };
        }

        // Inline-showdown variants that have already been awarded need to reconstruct
        // the response from persisted data so that every caller sees the same result.
        if (flowHandler.SupportsInlineShowdown && isAlreadyAwarded)
        {
            return await ReconstructInlineShowdownResultAsync(
                context, game, currentHandPots, usersByEmail, cancellationToken);
        }

        static bool ShouldSettleTerminalScrewYourNeighborVariant(Game game, ShowdownResult showdownResult)
        {
            if (game.Status == GameStatus.Completed)
            {
                return true;
            }

            return game.IsDealersChoice &&
                   string.Equals(game.CurrentHandGameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase) &&
                   showdownResult.WinnerPlayerIds.Count == 1 &&
                   game.GamePlayers.Count(gp => gp.Status == GamePlayerStatus.Active && gp.ChipStack > 0) <= 1;
        }

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

        var sharedCommunityCards = await context.GameCards
            .Where(c => c.GameId == command.GameId &&
                        c.HandNumber == game.CurrentHandNumber &&
                        !c.IsDiscarded &&
                        c.GamePlayerId == null &&
                        c.Location == CardLocation.Community)
            .OrderBy(c => c.DealOrder)
            .ToListAsync(cancellationToken);

        // Klondike: reveal the face-down Klondike Card at showdown
        if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var card in sharedCommunityCards.Where(c => c.DealtAtPhase == "KlondikeCard" && !c.IsVisible))
            {
                card.IsVisible = true;
            }
        }

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
        var includeSharedCommunityCards = UsesSharedCommunityCards(gameTypeCode);
        var pairPressureFaceUpCards = IsPairPressureGame(gameTypeCode)
            ? await GetOrderedFaceUpCardsAsync(context, game, cancellationToken)
            : null;
        var playerHandEvaluations = EvaluatePlayerHands(
            playersInHand,
            playerCardGroups,
            sharedCommunityCards,
            handEvaluator,
            includeSharedCommunityCards,
            gameTypeCode,
            pairPressureFaceUpCards);

        // 10. Award each pot to the best hand among ELIGIBLE players
        var (payouts, allWinners, overallWinReason) = await AwardPotsAsync(
            game, currentHandPots, playerHandEvaluations, isAlreadyAwarded, now, cancellationToken);

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

            // 11b. Record per-hand settlement to cashier ledger
            await handSettlementService.SettleHandAsync(game, payouts, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            if (engineOptions.Value.Enabled)
                await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);

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
            playerHandEvaluations, allWinners, payouts, usersByEmail, handEvaluator,
            sharedCommunityCards, includeSharedCommunityCards, gameTypeCode);

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

            // Settle hand results to cashier ledger (win-by-fold)
            var foldPayouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } };
            await handSettlementService.SettleHandAsync(game, foldPayouts, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            if (engineOptions.Value.Enabled)
                await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);

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
                    PlayerFirstName = ResolvePlayerFirstName(winnerUser, winner.Player.Name),
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
                // No settlement — chips are "in limbo" until pot is eventually awarded
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
            else
            {
                // Non-carry-forward dead hand: each player loses their contribution
                await handSettlementService.SettleHandAsync(game, new Dictionary<string, int>(), cancellationToken);
            }

            game.CurrentPhase = nameof(Phases.Complete);
            game.UpdatedAt = now;
            game.HandCompletedAt = now;
            game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
            MoveDealer(game);

            await context.SaveChangesAsync(cancellationToken);

            if (engineOptions.Value.Enabled)
                await gameStateManager.GetOrLoadGameAsync(game.Id, cancellationToken);
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
        IReadOnlyCollection<GameCard> sharedCommunityCards,
        IHandEvaluator handEvaluator,
        bool includeSharedCommunityCards,
        string gameTypeCode,
        IReadOnlyCollection<Card>? pairPressureFaceUpCards)
    {
        var evaluations = new Dictionary<string, PlayerHandEvaluation>();

        foreach (var gamePlayer in playersInHand)
        {
            if (!playerCardGroups.TryGetValue(gamePlayer.Id, out var cards))
            {
                continue; // Skip players without valid hands
            }

            HandBase hand;
            if (IsPairPressureGame(gameTypeCode))
            {
                hand = CreatePairPressureHand(cards, pairPressureFaceUpCards);
                if (hand is null)
                {
                    continue;
                }
            }
            else if (handEvaluator.SupportsPositionalCards)
            {
                // For positional-card games, separate private and visible cards.
                var holeCards = cards
                    .Where(c => c.Location == CardLocation.Hole || c.Location == CardLocation.Hand)
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                if (string.Equals(gamePlayer.Game.CurrentHandGameTypeCode ?? gamePlayer.Game.GameType?.Code, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase))
                {
                    var selectedShowcaseDealOrder = BobBarkerVariantState.GetSelectedShowcaseDealOrder(gamePlayer);
                    if (selectedShowcaseDealOrder.HasValue)
                    {
                        holeCards = cards
                            .Where(c => (c.Location == CardLocation.Hole || c.Location == CardLocation.Hand)
                                && c.DealOrder != selectedShowcaseDealOrder.Value)
                            .OrderBy(c => c.DealOrder)
                            .Select(MapToCard)
                            .ToList();
                    }
                }

                var boardCards = cards
                    .Where(c => c.Location == CardLocation.Board)
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                if (includeSharedCommunityCards && sharedCommunityCards.Count != 0)
                {
                    boardCards.AddRange(sharedCommunityCards
                        .OrderBy(c => c.DealOrder)
                        .Select(MapToCard));
                }

                if (holeCards.Count + boardCards.Count < 5)
                {
                    continue;
                }

                if (includeSharedCommunityCards)
                {
                    // Hold'em/Omaha: pass all private cards and shared board cards.
                    // Omaha evaluator enforces exact-two-hole usage internally.
                    hand = handEvaluator.CreateHand(holeCards, boardCards, []);
                }
                else
                {
                    // Stud/GBU/Baseball style: preserve existing split of initial hole and down cards.
                    var downCards = cards
                        .Where(c => c.Location == CardLocation.Hole)
                        .OrderBy(c => c.DealOrder)
                        .Skip(2) // First 2 are initial hole cards
                        .Select(c => MapToCard(c))
                        .ToList();

                    hand = handEvaluator.CreateHand(holeCards.Take(2).ToList(), boardCards, downCards);
                }
            }
            else
            {
                // For draw-style games, just pass all cards
                var coreCards = cards
                    .OrderBy(c => c.DealOrder)
                    .Select(c => MapToCard(c))
                    .ToList();

                if (coreCards.Count < 5)
                {
                    continue;
                }

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
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payouts = new Dictionary<string, int>();
        var allWinners = new HashSet<string>();
        string? overallWinReason = null;
        var gameTypeCode = game.CurrentHandGameTypeCode ?? game.GameType?.Code;
        var isBobBarker = string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase);
        var bobBarkerDealerCard = isBobBarker
            ? await context.GameCards
                .Where(c => c.GameId == game.Id
                    && c.HandNumber == game.CurrentHandNumber
                    && c.Location == CardLocation.Community
                    && c.GamePlayerId == null
                    && !c.IsDiscarded)
                .OrderBy(c => c.DealOrder)
                .AsNoTracking()
                .FirstOrDefaultAsync()
            : null;
        var bobBarkerDealerValue = bobBarkerDealerCard is null
            ? (int?)null
            : GetBobBarkerCardValue(bobBarkerDealerCard.Symbol, bobBarkerDealerCard.Symbol == CardSymbol.Ace);

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

                var potPayoutsByPlayer = new Dictionary<string, PotWinnerBreakdown>(StringComparer.OrdinalIgnoreCase);

                void AwardAmount(IReadOnlyList<string> winners, int amount, bool isHighHand, bool isShowcase)
                {
                    if (amount <= 0 || winners.Count == 0)
                    {
                        return;
                    }

                    var payoutPerWinner = amount / winners.Count;
                    var payoutRemainder = amount % winners.Count;

                    foreach (var winner in winners)
                    {
                        var payout = payoutPerWinner;
                        if (payoutRemainder > 0)
                        {
                            payout++;
                            payoutRemainder--;
                        }

                        if (payouts.TryGetValue(winner, out var existingPayout))
                        {
                            payouts[winner] = existingPayout + payout;
                        }
                        else
                        {
                            payouts[winner] = payout;
                        }

                        allWinners.Add(winner);

                        if (!potPayoutsByPlayer.TryGetValue(winner, out var breakdown))
                        {
                            breakdown = new PotWinnerBreakdown
                            {
                                GamePlayer = eligibleHands[winner].GamePlayer
                            };
                            potPayoutsByPlayer[winner] = breakdown;
                        }

                        breakdown.Total += payout;
                        if (isHighHand)
                        {
                            breakdown.HighHandAmount += payout;
                        }

                        if (isShowcase)
                        {
                            breakdown.ShowcaseAmount += payout;
                        }
                    }
                }

                string winReason;
                var winningHandDescription = FormatHandTypeForDisplay(gameTypeCode, eligibleHands[potWinners[0]].Hand);

                if (isBobBarker)
                {
                    var mainHandAmount = (pot.Amount + 1) / 2;
                    var showcaseAmount = pot.Amount - mainHandAmount;

                    AwardAmount(potWinners, mainHandAmount, isHighHand: true, isShowcase: false);

                    var showcaseWinners = bobBarkerDealerValue.HasValue
                        ? GetBobBarkerShowcaseWinners(eligibleHands, bobBarkerDealerValue.Value)
                        : [];

                    var showcaseRolledIntoMainHand = showcaseWinners.Count == 0;
                    if (showcaseRolledIntoMainHand)
                    {
                        showcaseWinners = potWinners;
                    }

                    AwardAmount(showcaseWinners, showcaseAmount, isHighHand: false, isShowcase: true);

                    winReason = showcaseRolledIntoMainHand
                        ? $"Bob Barker split pot - {winningHandDescription} (no showcase qualifier)"
                        : $"Bob Barker split pot - {winningHandDescription}";
                }
                else
                {
                    AwardAmount(potWinners, pot.Amount, isHighHand: false, isShowcase: false);
                    winReason = potWinners.Count > 1
                        ? $"Split pot - {winningHandDescription}"
                        : winningHandDescription;
                }

                var potPayoutsList = potPayoutsByPlayer.Values
                    .Select(payout => new
                    {
                        playerId = payout.GamePlayer.PlayerId.ToString(),
                        playerName = payout.GamePlayer.Player.Name,
                        amount = payout.Total,
                        highHandAmount = payout.HighHandAmount,
                        showcaseAmount = payout.ShowcaseAmount
                    })
                    .ToList();

                // Mark pot as awarded
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
        IHandEvaluator handEvaluator,
        IReadOnlyCollection<GameCard> sharedCommunityCards,
        bool includeSharedCommunityCards,
        string? gameTypeCode)
    {
        return playerHandEvaluations.Select(kvp =>
        {
            var isWinner = allWinners.Contains(kvp.Key);
            usersByEmail.TryGetValue(kvp.Value.GamePlayer.Player.Email ?? string.Empty, out var user);

            // Build the card list for display
            // For community card games (Hold'em, Omaha), include both hole cards and community cards
            var displayCards = kvp.Value.Cards
                .OrderBy(c => c.DealOrder)
                .Select(c => new ShowdownCard
                {
                    Suit = c.Suit,
                    Symbol = c.Symbol
                }).ToList();

            List<int>? bestCardIndexes = null;

            if (includeSharedCommunityCards && sharedCommunityCards.Count > 0)
            {
                // Append community cards to the display list
                var communityCardsForDisplay = string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase)
                    ? sharedCommunityCards.Where(c => c.IsVisible)
                    : sharedCommunityCards;

                var communityShowdownCards = communityCardsForDisplay
                    .OrderBy(c => c.DealOrder)
                    .Select(c => new ShowdownCard
                    {
                        Suit = c.Suit,
                        Symbol = c.Symbol
                    }).ToList();
                displayCards.AddRange(communityShowdownCards);

                // Compute best 5-card indexes from the combined card list
                // The hand object already has hole + community cards and knows the correct evaluation
                bestCardIndexes = FindBestCardIndexes(kvp.Value.Hand, displayCards);
            }

            // Generate hand description
            var handDescription = string.Equals(gameTypeCode, PokerGameMetadataRegistry.RazzCode, StringComparison.OrdinalIgnoreCase)
                && kvp.Value.Hand is RazzHand razzHand
                    ? RazzHand.GetLowHandDescription(razzHand.GetBestLowHand())
                    : HandDescriptionFormatter.GetHandDescription(kvp.Value.Hand);

            return new ShowdownPlayerHand
            {
                PlayerName = kvp.Key,
                PlayerFirstName = ResolvePlayerFirstName(user, kvp.Key),
                Cards = displayCards,
                HandType = FormatHandTypeForDisplay(gameTypeCode, kvp.Value.Hand),
                HandDescription = handDescription,
                HandStrength = kvp.Value.Hand.Strength,
                IsWinner = isWinner,
                AmountWon = payouts.GetValueOrDefault(kvp.Key, 0),
                BestCardIndexes = bestCardIndexes
            };
        }).OrderByDescending(h => h.HandStrength ?? 0).ToList();
    }

    private static string FormatHandTypeForDisplay(string? gameTypeCode, HandBase hand)
    {
        if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.RazzCode, StringComparison.OrdinalIgnoreCase)
            && hand is RazzHand razzHand)
        {
            return RazzHand.GetLowHandDescription(razzHand.GetBestLowHand());
        }

        return hand.Type.ToString();
    }

    /// <summary>
    /// Finds the indexes of the best 5-card hand within the display card list.
    /// Works for community card games (Hold'em, Omaha) that have constraints on
    /// which combinations of hole and community cards form valid hands.
    /// </summary>
    private static List<int>? FindBestCardIndexes(HandBase hand, List<ShowdownCard> displayCards)
    {
        if (displayCards.Count <= 5)
        {
            return null; // No need for indexes when showing all cards
        }

        // Convert display cards to core Card objects for evaluation
        var allCoreCards = displayCards
            .Select(c => new Card(MapSuit(c.Suit), MapSymbol(c.Symbol)))
            .ToList();

        // Determine hole/community split based on hand type
        if (hand is CommunityCardsHand communityHand)
        {
            // Use the hand's own constraints to find the best combo
            var holeCards = communityHand.HoleCards.ToList();
            var communityCards = communityHand.CommunityCards.ToList();

            var ranking = HandTypeStrengthRanking.Classic;
            List<Card>? bestCombo = null;
            long bestStrength = long.MinValue;

            // Omaha: exactly 2 hole + 3 community
            // Nebraska: exactly 3 hole + 2 community
            // Hold'em style: 0-5 hole, constrained by available community cards.
            var minHole = communityHand switch
            {
                OmahaHand => 2,
                BobBarkerHand => 2,
                NebraskaHand => 3,
                _ => 0
            };
            var maxHole = communityHand switch
            {
                OmahaHand => 2,
                BobBarkerHand => 2,
                NebraskaHand => 3,
                _ => Math.Min(holeCards.Count, 5)
            };

            for (var numHole = minHole; numHole <= maxHole; numHole++)
            {
                var numCommunity = 5 - numHole;
                if (numCommunity > communityCards.Count) continue;

                foreach (var holeSub in holeCards.SubsetsOfSize(numHole))
                {
                    foreach (var commSub in communityCards.SubsetsOfSize(numCommunity))
                    {
                        var combo = holeSub.Concat(commSub).ToList();
                        var handType = HandTypeDetermination.DetermineHandType(combo);
                        var strength = HandStrength.Calculate(combo, handType, ranking);
                        if (strength > bestStrength)
                        {
                            bestStrength = strength;
                            bestCombo = combo;
                        }
                    }
                }
            }

            if (bestCombo is not null)
            {
                return GetCardIndexes(allCoreCards, bestCombo);
            }
        }

        // Fallback: generic best-5 from all cards
        return GetCardIndexes(allCoreCards, FindBestFiveCardHand(allCoreCards));
    }

    /// <summary>
    /// Gets the zero-based indexes of target cards within the full card list.
    /// </summary>
    private static List<int> GetCardIndexes(List<Card> allCards, IEnumerable<Card> targetCards)
    {
        var indexes = new List<int>();
        var usedIndexes = new HashSet<int>();

        foreach (var target in targetCards)
        {
            for (var i = 0; i < allCards.Count; i++)
            {
                if (usedIndexes.Contains(i)) continue;

                if (allCards[i].Equals(target))
                {
                    indexes.Add(i);
                    usedIndexes.Add(i);
                    break;
                }
            }
        }

        return indexes;
    }

    /// <summary>
    /// Finds the best 5-card hand from a set of cards by evaluating all C(n,5) combinations.
    /// Used as a fallback for non-constrained hand types.
    /// </summary>
    private static List<Card> FindBestFiveCardHand(List<Card> allCards)
    {
        if (allCards.Count <= 5)
        {
            return allCards;
        }

        var ranking = HandTypeStrengthRanking.Classic;
        List<Card>? bestCombo = null;
        long bestStrength = long.MinValue;

        foreach (var combo in allCards.SubsetsOfSize(5))
        {
            var comboList = combo.ToList();
            var handType = HandTypeDetermination.DetermineHandType(comboList);
            var strength = HandStrength.Calculate(comboList, handType, ranking);
            if (strength > bestStrength)
            {
                bestStrength = strength;
                bestCombo = comboList;
            }
        }

        return bestCombo ?? allCards.Take(5).ToList();
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

    private static bool IsPairPressureGame(string gameTypeCode)
        => string.Equals(gameTypeCode, PokerGameMetadataRegistry.PairPressureCode, StringComparison.OrdinalIgnoreCase);

    private static PairPressureHand? CreatePairPressureHand(
        List<GameCard> cards,
        IReadOnlyCollection<Card>? pairPressureFaceUpCards)
    {
        if (pairPressureFaceUpCards is null)
        {
            return null;
        }

        var holeCards = cards
            .Where(c => c.Location == CardLocation.Hole || c.Location == CardLocation.Hand)
            .OrderBy(c => c.DealOrder)
            .Select(MapToCard)
            .ToList();
        var boardCards = cards
            .Where(c => c.Location == CardLocation.Board)
            .OrderBy(c => c.DealOrder)
            .Select(MapToCard)
            .ToList();

        if (holeCards.Count < 2)
        {
            return null;
        }

        var initialHoleCards = holeCards.Take(2).ToList();
        var downCard = holeCards.Skip(2).FirstOrDefault();
        return new PairPressureHand(initialHoleCards, boardCards, downCard, pairPressureFaceUpCards);
    }

    private static async Task<IReadOnlyCollection<Card>> GetOrderedFaceUpCardsAsync(
        CardsDbContext context,
        Game game,
        CancellationToken cancellationToken)
    {
        var faceUpCards = await context.GameCards
            .Where(c => c.GameId == game.Id
                        && c.HandNumber == game.CurrentHandNumber
                        && c.IsVisible
                        && !c.IsDiscarded)
            .Include(c => c.GamePlayer)
            .Select(c => new
            {
                c.Symbol,
                c.Suit,
                c.DealtAtPhase,
                c.DealOrder,
                SeatPosition = c.GamePlayer != null ? c.GamePlayer.SeatPosition : -1
            })
            .ToListAsync(cancellationToken);

        return faceUpCards
            .OrderBy(c => GetStreetPhaseOrder(c.DealtAtPhase))
            .ThenBy(c => c.DealOrder)
            .ThenBy(c => c.SeatPosition > game.DealerPosition ? c.SeatPosition : c.SeatPosition + 1000)
            .Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
            .ToList();
    }

    private static int GetStreetPhaseOrder(string? phase) => phase switch
    {
        nameof(Phases.ThirdStreet) => 1,
        nameof(Phases.FourthStreet) => 2,
        nameof(Phases.FifthStreet) => 3,
        nameof(Phases.SixthStreet) => 4,
        nameof(Phases.SeventhStreet) => 5,
        _ => 999
    };

    private static bool UsesSharedCommunityCards(string gameTypeCode)
    {
        return string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.OmahaCode, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode, StringComparison.OrdinalIgnoreCase)
               || string.Equals(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> GetBobBarkerShowcaseWinners(
        Dictionary<string, PlayerHandEvaluation> eligibleHands,
        int dealerCardValue)
    {
        var qualifiedShowcaseValues = eligibleHands
            .Select(kvp => new
            {
                PlayerName = kvp.Key,
                ShowcaseCard = GetBobBarkerShowcaseCard(kvp.Value.GamePlayer, kvp.Value.Cards)
            })
            .Select(x => new
            {
                x.PlayerName,
                Value = GetBobBarkerCardValue(x.ShowcaseCard!.Symbol, dealerCardValue == 14)
            })
            .Where(x => x.Value <= dealerCardValue)
            .ToList();

        if (qualifiedShowcaseValues.Count == 0)
        {
            return [];
        }

        var bestValue = qualifiedShowcaseValues.Max(x => x.Value);
        return qualifiedShowcaseValues
            .Where(x => x.Value == bestValue)
            .Select(x => x.PlayerName)
            .ToList();
    }

    private static GameCard? GetBobBarkerShowcaseCard(GamePlayer gamePlayer, IReadOnlyCollection<GameCard> cards)
    {
        var selectedShowcaseDealOrder = BobBarkerVariantState.GetSelectedShowcaseDealOrder(gamePlayer);
        if (!selectedShowcaseDealOrder.HasValue)
        {
            return null;
        }

        return cards.FirstOrDefault(c => c.DealOrder == selectedShowcaseDealOrder.Value);
    }

    private static int GetBobBarkerCardValue(CardSymbol symbol, bool aceHigh)
    {
        if (symbol == CardSymbol.Ace)
        {
            return aceHigh ? 14 : 1;
        }

        return (int)symbol;
    }

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

    private sealed class PotWinnerBreakdown
    {
        public required GamePlayer GamePlayer { get; init; }
        public int Total { get; set; }
        public int HighHandAmount { get; set; }
        public int ShowcaseAmount { get; set; }
    }

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
    /// Reconstructs the inline showdown response from persisted data when pots
    /// have already been awarded by a prior caller, avoiding the race condition
    /// where the second caller skips the live showdown branch.
    /// </summary>
    private async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> ReconstructInlineShowdownResultAsync(
        CardsDbContext context,
        Game game,
        List<Data.Entities.Pot> currentHandPots,
        Dictionary<string, UserInfo> usersByEmail,
        CancellationToken cancellationToken)
    {
        // Determine winners and payouts from the already-awarded pot's WinnerPayouts JSON.
        var winnerPlayerIds = new HashSet<Guid>();
        var payoutsByName = new Dictionary<string, int>();

        var awardedPot = currentHandPots.FirstOrDefault(p => p.IsAwarded && !string.IsNullOrWhiteSpace(p.WinnerPayouts));
        if (awardedPot?.WinnerPayouts is not null)
        {
            using var doc = JsonDocument.Parse(awardedPot.WinnerPayouts);
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (entry.TryGetProperty("playerId", out var pidProp) &&
                    Guid.TryParse(pidProp.GetString(), out var pid))
                {
                    winnerPlayerIds.Add(pid);
                }

                var name = entry.TryGetProperty("playerName", out var nameProp) ? nameProp.GetString() : null;
                var amount = entry.TryGetProperty("amount", out var amtProp) && amtProp.TryGetInt32(out var amt) ? amt : 0;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    payoutsByName[name] = amount;
                }
            }
        }

        // Load cards for all players in this hand.
        var handCards = await context.GameCards
            .Where(c => c.GameId == game.Id &&
                        c.HandNumber == game.CurrentHandNumber &&
                        !c.IsDiscarded &&
                        c.GamePlayerId != null &&
                        c.Location == CardLocation.Hand)
            .OrderBy(c => c.DealOrder)
            .ToListAsync(cancellationToken);

        var cardsByGamePlayerId = handCards
            .Where(c => c.GamePlayerId.HasValue)
            .GroupBy(c => c.GamePlayerId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build PlayerHands for every game player that held cards this hand.
        var gamePlayerIdsWithCards = cardsByGamePlayerId.Keys.ToHashSet();
        var participants = game.GamePlayers
            .Where(gp => gamePlayerIdsWithCards.Contains(gp.Id))
            .ToList();

        var playerHands = participants
            .Select(gp =>
            {
                usersByEmail.TryGetValue(gp.Player.Email ?? string.Empty, out var user);

                var canonicalName = gp.Player.Name;
                var firstName = ResolvePlayerFirstName(user, canonicalName);

                var cards = cardsByGamePlayerId.TryGetValue(gp.Id, out var playerCards)
                    ? playerCards.Select(c => new ShowdownCard { Suit = c.Suit, Symbol = c.Symbol }).ToList()
                    : new List<ShowdownCard>();

                return new ShowdownPlayerHand
                {
                    PlayerName = canonicalName,
                    PlayerFirstName = firstName,
                    Cards = cards,
                    HandType = null,
                    HandDescription = null,
                    HandStrength = null,
                    IsWinner = winnerPlayerIds.Contains(gp.PlayerId),
                    AmountWon = payoutsByName.GetValueOrDefault(canonicalName, 0)
                };
            })
            .ToList();

        return new PerformShowdownSuccessful
        {
            GameId = game.Id,
            WonByFold = false,
            CurrentPhase = game.CurrentPhase,
            Payouts = payoutsByName,
            PlayerHands = playerHands,
            ShouldBroadcastGameState = false
        };
    }

    /// <summary>
    /// Resolves the display first name for a player at showdown.
    /// Prefers Identity FirstName; falls back to TitleCased email local-part.
    /// </summary>
    private static string? ResolvePlayerFirstName(UserInfo? user, string canonicalName)
    {
        var firstName = user?.FirstName?.Trim();
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            return firstName;
        }

        // Derive from email local-part when Identity profile is missing
        var emailSource = !string.IsNullOrWhiteSpace(user?.Email) ? user.Email : canonicalName;
        var atIndex = emailSource?.IndexOf('@') ?? -1;
        if (atIndex > 0)
        {
            var localPart = emailSource![..atIndex];
            var segments = localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(segments[0].ToLowerInvariant());
            }
        }

        return null;
    }

    /// <summary>
    /// Holds evaluation results for a player's hand.
    /// </summary>
    private sealed record PlayerHandEvaluation(HandBase Hand, List<GameCard> Cards, GamePlayer GamePlayer);

    /// <summary>
    /// Holds user information for display purposes.
    /// </summary>
    private sealed record UserInfo(string Email, string? FirstName, string? LastName);
}


