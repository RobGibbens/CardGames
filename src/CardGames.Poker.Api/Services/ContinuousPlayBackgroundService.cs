using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.FiveCardDraw;
using Microsoft.EntityFrameworkCore;

//TODO:ROB - This should not be tied to FiveCardDraw - make it generic for all poker variants
namespace CardGames.Poker.Api.Services;

/// <summary>
/// Background service that monitors games for continuous play and automatically
/// starts new hands after the results display period expires.
/// </summary>
public sealed class ContinuousPlayBackgroundService : BackgroundService
{
    /// <summary>
    /// Duration in seconds for the results display period before starting the next hand.
    /// </summary>
    public const int ResultsDisplayDurationSeconds = 15;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContinuousPlayBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuousPlayBackgroundService"/> class.
    /// </summary>
    public ContinuousPlayBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ContinuousPlayBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ContinuousPlayBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessGamesReadyForNextHandAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing continuous play games");
            }

                    await Task.Delay(_checkInterval, stoppingToken);
                }

                _logger.LogInformation("ContinuousPlayBackgroundService stopped");
            }

            private async Task ProcessGamesReadyForNextHandAsync(CancellationToken cancellationToken)
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
                var broadcaster = scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>();

                var now = DateTimeOffset.UtcNow;

                // Check for abandoned games (all players left after game started)
                await ProcessAbandonedGamesAsync(context, broadcaster, now, cancellationToken);

                // Find games in Complete phase where the next hand should start
                var gamesReadyForNextHand = await context.Games
                    .Where(g => g.CurrentPhase == nameof(FiveCardDrawPhase.Complete) &&
                                g.NextHandStartsAt != null &&
                                g.NextHandStartsAt <= now &&
                                g.Status == GameStatus.InProgress)
                    .Include(g => g.GamePlayers)
                    .ToListAsync(cancellationToken);

                foreach (var game in gamesReadyForNextHand)
                {
                    try
                    {
                        await StartNextHandAsync(context, broadcaster, game, now, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start next hand for game {GameId}", game.Id);
                    }
                }
            }

            /// <summary>
            /// Checks for games where all players have left after the game started,
            /// and marks them as complete.
            /// </summary>
            private async Task ProcessAbandonedGamesAsync(
                CardsDbContext context,
                IGameStateBroadcaster broadcaster,
                DateTimeOffset now,
                CancellationToken cancellationToken)
            {
                // Find in-progress games (not waiting phases)
                var inProgressPhases = new[]
                {
                    nameof(FiveCardDrawPhase.CollectingAntes),
                    nameof(FiveCardDrawPhase.Dealing),
                    nameof(FiveCardDrawPhase.FirstBettingRound),
                    nameof(FiveCardDrawPhase.DrawPhase),
                    nameof(FiveCardDrawPhase.SecondBettingRound),
                    nameof(FiveCardDrawPhase.Showdown),
                    nameof(FiveCardDrawPhase.Complete)
                };

                var activeGames = await context.Games
                    .Where(g => inProgressPhases.Contains(g.CurrentPhase) &&
                                (g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands))
                    .Include(g => g.GamePlayers)
                    .ToListAsync(cancellationToken);

                foreach (var game in activeGames)
                {
                    // Check if all players have left (no active, connected players remaining)
                    var activePlayers = game.GamePlayers
                        .Where(gp => gp.Status == GamePlayerStatus.Active &&
                                     gp.LeftAtHandNumber == -1)
                        .ToList();

                    if (activePlayers.Count == 0)
                    {
                        _logger.LogInformation(
                            "Game {GameId} has no remaining players, marking as complete",
                            game.Id);

                        game.CurrentPhase = nameof(FiveCardDrawPhase.Complete);
                        game.Status = GameStatus.Completed;
                        game.EndedAt = now;
                        game.UpdatedAt = now;
                        game.NextHandStartsAt = null;
                        game.HandCompletedAt = null;

                        await context.SaveChangesAsync(cancellationToken);
                        await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
                    }
                }
            }

    private async Task StartNextHandAsync(
        CardsDbContext context,
        IGameStateBroadcaster broadcaster,
        Game game,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Check for eligible players (occupied, not sitting out, chips >= ante, hasn't left)
        var ante = game.Ante ?? 0;
        var eligiblePlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         gp.ChipStack >= ante &&
                         gp.LeftAtHandNumber == -1)
            .ToList();

        // Auto-sit-out players with insufficient chips
        var insufficientChipPlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         gp.ChipStack < ante &&
                         gp.ChipStack > 0 &&
                         gp.LeftAtHandNumber == -1)
            .ToList();

        foreach (var player in insufficientChipPlayers)
        {
            player.IsSittingOut = true;
            _logger.LogInformation(
                "Player {PlayerName} auto-sat-out due to insufficient chips ({Chips} < {Ante}) in game {GameId}",
                player.Player?.Name ?? player.PlayerId.ToString(),
                player.ChipStack,
                ante,
                game.Id);
        }

        // Check if all players have left - end the game
        var remainingPlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber == -1)
            .ToList();

        if (remainingPlayers.Count == 0)
        {
            _logger.LogInformation(
                "Game {GameId} has no remaining players, ending game",
                game.Id);

            game.CurrentPhase = nameof(FiveCardDrawPhase.Complete);
            game.Status = GameStatus.Completed;
            game.EndedAt = now;
            game.NextHandStartsAt = null;
            game.HandCompletedAt = null;
            game.UpdatedAt = now;

            await context.SaveChangesAsync(cancellationToken);
            await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
            return;
        }

        // Check minimum player count
        if (eligiblePlayers.Count < 2)
        {
            _logger.LogInformation(
                "Game {GameId} has insufficient eligible players ({Count}), pausing continuous play",
                game.Id,
                eligiblePlayers.Count);

            // Pause continuous play - stay in Complete phase but clear schedule
            game.NextHandStartsAt = null;
            game.Status = GameStatus.BetweenHands;
            game.UpdatedAt = now;

                await context.SaveChangesAsync(cancellationToken);
                await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
                return;
            }

            // Reset player states for new hand
            foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
            {
                gamePlayer.CurrentBet = 0;
                gamePlayer.TotalContributedThisHand = 0;
                gamePlayer.HasFolded = false;
                gamePlayer.IsAllIn = false;
                gamePlayer.HasDrawnThisRound = false;
            }

            // Remove any existing cards from previous hand
            var existingCards = await context.GameCards
                .Where(gc => gc.GameId == game.Id)
                .ToListAsync(cancellationToken);

            if (existingCards.Count > 0)
            {
                context.GameCards.RemoveRange(existingCards);
            }

            // Mark any incomplete betting rounds from the previous hand as complete
            var incompleteBettingRounds = await context.Set<Data.Entities.BettingRound>()
                .Where(br => br.GameId == game.Id && !br.IsComplete)
                .ToListAsync(cancellationToken);

            foreach (var br in incompleteBettingRounds)
            {
                br.IsComplete = true;
                br.CompletedAt = now;
            }

            // Create a new main pot for this hand
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

            // NOTE: Dealer rotation is already done in PerformShowdownCommandHandler.MoveDealer()
            // when the previous hand completes. We do NOT rotate again here.

            // Update game state
            game.CurrentHandNumber++;
            game.CurrentPhase = nameof(FiveCardDrawPhase.CollectingAntes);
            game.Status = GameStatus.InProgress;
            game.CurrentPlayerIndex = -1;
            game.CurrentDrawPlayerIndex = -1;
            game.HandCompletedAt = null;
            game.NextHandStartsAt = null;
        game.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started hand {HandNumber} for game {GameId} with {PlayerCount} eligible players. Dealer at seat {DealerPosition}",
            game.CurrentHandNumber,
            game.Id,
            eligiblePlayers.Count,
            game.DealerPosition);

        // Automatically collect antes
        await CollectAntesAsync(context, game, eligiblePlayers, ante, now, cancellationToken);

            // Automatically deal hands
            await DealHandsAsync(context, game, eligiblePlayers, now, cancellationToken);

            // Broadcast updated state
            await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
        }

        private async Task CollectAntesAsync(
            CardsDbContext context,
            Game game,
            List<GamePlayer> eligiblePlayers,
            int ante,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            if (ante <= 0)
            {
                game.CurrentPhase = nameof(FiveCardDrawPhase.Dealing);
                return;
            }

            var pot = await context.Pots
                .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                          p.HandNumber == game.CurrentHandNumber &&
                                          p.PotType == PotType.Main,
                                 cancellationToken);

        foreach (var player in eligiblePlayers)
        {
            var anteAmount = Math.Min(ante, player.ChipStack);
            player.ChipStack -= anteAmount;
            player.CurrentBet = anteAmount;
            player.TotalContributedThisHand = anteAmount;

            if (pot is not null)
            {
                pot.Amount += anteAmount;

                var contribution = new PotContribution
                {
                    PotId = pot.Id,
                    GamePlayerId = player.Id,
                    Amount = anteAmount,
                    ContributedAt = now
                };
                context.Set<PotContribution>().Add(contribution);
            }

            if (player.ChipStack == 0)
            {
                player.IsAllIn = true;
            }
        }

        game.CurrentPhase = nameof(FiveCardDrawPhase.Dealing);
        game.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task DealHandsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Create a shuffled deck
        var random = new Random();
        var deck = CreateShuffledDeck(random);
        var deckIndex = 0;

        // Sort players starting from left of dealer for dealing order
        var dealerPosition = game.DealerPosition;
        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1; // Seats are 0-indexed

        var playersInDealOrder = eligiblePlayers
            .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
            .ToList();

        // Deal 5 cards to each player
        foreach (var player in playersInDealOrder)
        {
            for (var cardIndex = 0; cardIndex < 5; cardIndex++)
            {
                if (deckIndex >= deck.Count)
                {
                    _logger.LogError("Ran out of cards while dealing for game {GameId}", game.Id);
                    break;
                }

                var (suit, symbol) = deck[deckIndex++];
                var gameCard = new GameCard
                {
                    GameId = game.Id,
                    GamePlayerId = player.Id,
                    HandNumber = game.CurrentHandNumber,
                    Suit = suit,
                    Symbol = symbol,
                    DealOrder = cardIndex,
                    Location = CardLocation.Hand,
                    IsVisible = false,
                    IsDiscarded = false,
                    DealtAt = now
                };

                context.GameCards.Add(gameCard);
            }
        }

        // Reset CurrentBet for all players before first betting round
        // (ante contributions are tracked in TotalContributedThisHand)
        foreach (var player in game.GamePlayers)
        {
            player.CurrentBet = 0;
        }

        // Determine first actor (left of dealer among non-folded, non-all-in players)
        var firstActorIndex = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);

        // Create betting round record - this is required for ProcessBettingAction to work
        var bettingRound = new Data.Entities.BettingRound
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            RoundNumber = 1,
            Street = nameof(FiveCardDrawPhase.FirstBettingRound),
            CurrentBet = 0,
            MinBet = game.MinBet ?? 0,
            RaiseCount = 0,
            MaxRaises = 0, // Unlimited raises
            LastRaiseAmount = 0,
            PlayersInHand = eligiblePlayers.Count,
            PlayersActed = 0,
            CurrentActorIndex = firstActorIndex,
            LastAggressorIndex = -1,
            IsComplete = false,
            StartedAt = now
        };

        context.Set<Data.Entities.BettingRound>().Add(bettingRound);

        game.CurrentPhase = nameof(FiveCardDrawPhase.FirstBettingRound);
        game.CurrentPlayerIndex = firstActorIndex;
        game.UpdatedAt = now;

        _logger.LogInformation(
            "Dealt cards for game {GameId} hand {HandNumber}. First actor at seat {FirstActorIndex}, dealer at seat {DealerPosition}",
            game.Id,
            game.CurrentHandNumber,
            firstActorIndex,
            game.DealerPosition);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Finds the first active player after the dealer position who can act.
    /// </summary>
    private static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
    {
        if (activePlayers.Count == 0)
        {
            return -1;
        }

        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1;
        var searchIndex = (game.DealerPosition + 1) % totalSeats;

        // Search through all seat positions starting left of dealer
        for (var i = 0; i < totalSeats; i++)
        {
            var player = activePlayers.FirstOrDefault(p => p.SeatPosition == searchIndex);
            if (player is not null && !player.HasFolded && !player.IsAllIn)
            {
                return searchIndex;
            }
            searchIndex = (searchIndex + 1) % totalSeats;
        }

        return -1; // No active player found
    }

    private static List<(Data.Entities.CardSuit Suit, Data.Entities.CardSymbol Symbol)> CreateShuffledDeck(Random random)
    {
        var suits = new[]
        {
            Data.Entities.CardSuit.Clubs,
            Data.Entities.CardSuit.Diamonds,
            Data.Entities.CardSuit.Hearts,
            Data.Entities.CardSuit.Spades
        };

        var symbols = new[]
        {
            Data.Entities.CardSymbol.Deuce,
            Data.Entities.CardSymbol.Three,
            Data.Entities.CardSymbol.Four,
            Data.Entities.CardSymbol.Five,
            Data.Entities.CardSymbol.Six,
            Data.Entities.CardSymbol.Seven,
            Data.Entities.CardSymbol.Eight,
            Data.Entities.CardSymbol.Nine,
            Data.Entities.CardSymbol.Ten,
            Data.Entities.CardSymbol.Jack,
            Data.Entities.CardSymbol.Queen,
            Data.Entities.CardSymbol.King,
            Data.Entities.CardSymbol.Ace
        };

        var deck = new List<(Data.Entities.CardSuit, Data.Entities.CardSymbol)>();
        foreach (var suit in suits)
        {
            foreach (var symbol in symbols)
            {
                deck.Add((suit, symbol));
            }
        }

        // Fisher-Yates shuffle
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }
}
