using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Base implementation of <see cref="IGameFlowHandler"/> with common poker game logic.
/// Game-specific handlers inherit from this and override specific behaviors.
/// </summary>
/// <remarks>
/// <para>
/// This base class provides default implementations for common poker operations:
/// </para>
/// <list type="bullet">
///   <item><description>Initial phase transitions (defaults to CollectingAntes)</description></item>
///   <item><description>Sequential phase navigation based on GameRules.Phases</description></item>
///   <item><description>Helper methods for phase categorization</description></item>
/// </list>
/// <para>
/// Derived classes should override specific methods to customize game-specific behavior
/// while inheriting common logic.
/// </para>
/// </remarks>
public abstract class BaseGameFlowHandler : IGameFlowHandler
{
    /// <inheritdoc />
    public abstract string GameTypeCode { get; }

    /// <inheritdoc />
    public abstract GameRules GetGameRules();

    /// <inheritdoc />
    public virtual string GetInitialPhase(Game game)
    {
        // Default: Start with ante collection
        return nameof(Phases.CollectingAntes);
    }

    /// <inheritdoc />
    public virtual string? GetNextPhase(Game game, string currentPhase)
    {
        var rules = GetGameRules();
        var phases = rules.Phases;

        // Find the current phase index
        var currentIndex = -1;
        for (var i = 0; i < phases.Count; i++)
        {
            if (string.Equals(phases[i].PhaseId, currentPhase, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        // If current phase not found or is the last phase, no transition
        if (currentIndex < 0 || currentIndex >= phases.Count - 1)
        {
            return null;
        }

        return phases[currentIndex + 1].PhaseId;
    }

    /// <inheritdoc />
    public abstract DealingConfiguration GetDealingConfiguration();

    /// <inheritdoc />
    public virtual bool SkipsAnteCollection => false;

    /// <inheritdoc />
    public virtual IReadOnlyList<string> SpecialPhases => [];

    /// <inheritdoc />
    public virtual Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special initialization
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special cleanup
        return Task.CompletedTask;
    }

    #region Dealing

    /// <inheritdoc />
    public virtual Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Default implementation for draw-style games
        return DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
    }

    /// <summary>
    /// Standard dealing implementation for draw-style games.
    /// Deals a fixed number of cards face-down to each player.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="eligiblePlayers">Players to receive cards.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    protected async Task DealDrawStyleCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var config = GetDealingConfiguration();
        var cardsPerPlayer = config.InitialCardsPerPlayer > 0 ? config.InitialCardsPerPlayer : 5;

        // Create a shuffled deck
        var deck = CreateShuffledDeck();

        // Persist all 52 cards with their shuffled order
        var deckCards = new List<GameCard>();
        var deckOrder = 0;
        foreach (var (suit, symbol) in deck)
        {
            var gameCard = new GameCard
            {
                GameId = game.Id,
                GamePlayerId = null,
                HandNumber = game.CurrentHandNumber,
                Suit = suit,
                Symbol = symbol,
                DealOrder = deckOrder++,
                Location = CardLocation.Deck,
                IsVisible = false,
                IsDiscarded = false,
                DealtAt = now
            };
            deckCards.Add(gameCard);
            context.GameCards.Add(gameCard);
        }

        var deckIndex = 0;

        // Sort players starting from left of dealer
        var dealerPosition = game.DealerPosition;
        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1;

        var playersInDealOrder = eligiblePlayers
            .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
            .ToList();

        // Deal cards to each player
        var dealOrder = 1;
        foreach (var player in playersInDealOrder)
        {
            for (var cardIndex = 0; cardIndex < cardsPerPlayer; cardIndex++)
            {
                if (deckIndex >= deckCards.Count) break;

                var card = deckCards[deckIndex++];
                card.GamePlayerId = player.Id;
                card.Location = CardLocation.Hand;
                card.DealOrder = dealOrder++;
                card.IsVisible = !config.AllFaceDown;
                card.DealtAt = now;
            }
        }

        // Reset CurrentBet for all players
        foreach (var player in game.GamePlayers)
        {
            player.CurrentBet = 0;
        }

        // Determine next phase and set up game state
        var nextPhase = GetNextPhase(game, nameof(Phases.Dealing));

        if (SpecialPhases.Contains(nextPhase ?? "", StringComparer.OrdinalIgnoreCase))
        {
            // Special phase - no betting round setup
            game.CurrentPhase = nextPhase!;
            game.CurrentPlayerIndex = -1;
        }
        else
        {
            // Standard poker - set up first betting round
            var firstActorIndex = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);

            var bettingRound = new Data.Entities.BettingRound
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                RoundNumber = 1,
                Street = nextPhase ?? nameof(Phases.FirstBettingRound),
                CurrentBet = 0,
                MinBet = game.MinBet ?? 0,
                RaiseCount = 0,
                MaxRaises = 0,
                LastRaiseAmount = 0,
                PlayersInHand = eligiblePlayers.Count,
                PlayersActed = 0,
                CurrentActorIndex = firstActorIndex,
                LastAggressorIndex = -1,
                IsComplete = false,
                StartedAt = now
            };

            context.Set<Data.Entities.BettingRound>().Add(bettingRound);

            game.CurrentPhase = nextPhase ?? nameof(Phases.FirstBettingRound);
            game.CurrentPlayerIndex = firstActorIndex;
        }

        game.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a shuffled 52-card deck.
    /// </summary>
    /// <returns>A list of (suit, symbol) tuples representing the shuffled deck.</returns>
    protected static List<(CardSuit Suit, CardSymbol Symbol)> CreateShuffledDeck()
    {
        var suits = new[]
        {
            CardSuit.Clubs,
            CardSuit.Diamonds,
            CardSuit.Hearts,
            CardSuit.Spades
        };

        var symbols = new[]
        {
            CardSymbol.Deuce,
            CardSymbol.Three,
            CardSymbol.Four,
            CardSymbol.Five,
            CardSymbol.Six,
            CardSymbol.Seven,
            CardSymbol.Eight,
            CardSymbol.Nine,
            CardSymbol.Ten,
            CardSymbol.Jack,
            CardSymbol.Queen,
            CardSymbol.King,
            CardSymbol.Ace
        };

        var deck = new List<(CardSuit, CardSymbol)>();
        foreach (var suit in suits)
        {
            foreach (var symbol in symbols)
            {
                deck.Add((suit, symbol));
            }
        }

        // Fisher-Yates shuffle
        var random = Random.Shared;
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }

    /// <summary>
    /// Finds the first active player after the dealer position who can act.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <param name="activePlayers">List of active players.</param>
    /// <returns>The seat position of the first actor, or -1 if none found.</returns>
    protected static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
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

        return -1;
    }

    #endregion

    #region Showdown

    /// <inheritdoc />
    public virtual bool SupportsInlineShowdown => false;

    /// <inheritdoc />
    public virtual Task<ShowdownResult> PerformShowdownAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Default: Use the generic PerformShowdownCommandHandler via MediatR
        // This is a fallback - games with inline showdown should override this
        throw new NotSupportedException(
            $"Inline showdown is not supported for {GameTypeCode}. " +
            $"Use the PerformShowdownCommand handler instead.");
    }

    #endregion

    #region Post-Phase Processing

    /// <inheritdoc />
    public virtual Task<string> ProcessDrawCompleteAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Default: Transition to second betting round
        return Task.FromResult(nameof(Phases.SecondBettingRound));
    }

    /// <inheritdoc />
    public virtual Task<string> ProcessPostShowdownAsync(
        CardsDbContext context,
        Game game,
        ShowdownResult showdownResult,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Default: Go directly to Complete phase
        return Task.FromResult(nameof(Phases.Complete));
    }

    #endregion

    #region Chip Check

    /// <inheritdoc />
    public virtual bool RequiresChipCoverageCheck => false;

    /// <inheritdoc />
    public virtual ChipCheckConfiguration GetChipCheckConfiguration() =>
        ChipCheckConfiguration.Disabled;

    #endregion

    /// <summary>
    /// Determines if the specified phase is a betting phase.
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Betting; otherwise, false.</returns>
    protected bool IsBettingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Betting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the specified phase is a drawing phase.
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Drawing; otherwise, false.</returns>
    protected bool IsDrawingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Drawing", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the specified phase is a resolution phase (Showdown, Complete).
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Resolution; otherwise, false.</returns>
    protected bool IsResolutionPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Resolution", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the phase descriptor for the specified phase ID.
    /// </summary>
    /// <param name="phaseId">The phase ID to look up.</param>
    /// <returns>The phase descriptor, or null if not found.</returns>
    protected GamePhaseDescriptor? GetPhaseDescriptor(string phaseId)
    {
        var rules = GetGameRules();
        return rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phaseId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Counts the number of active players who haven't folded.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The count of active, non-folded players.</returns>
    protected static int CountActivePlayers(Game game)
    {
        return game.GamePlayers
            .Count(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded && !gp.IsSittingOut);
    }

    /// <summary>
    /// Determines if only one player remains active (potential early win).
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>True if only one active player remains; otherwise, false.</returns>
    protected static bool IsSinglePlayerRemaining(Game game)
    {
        return CountActivePlayers(game) == 1;
    }
}
