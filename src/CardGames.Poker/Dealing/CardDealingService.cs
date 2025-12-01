using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Represents a card that has been dealt with visibility information.
/// </summary>
public record DealtCard(
    Card Card,
    string Recipient,
    DealCardType CardType,
    int DealSequence,
    bool IsFaceUp);

/// <summary>
/// Result of a dealing phase operation.
/// Contains all dealt cards with their visibility states.
/// </summary>
public record DealingPhaseResult(
    string PhaseName,
    IReadOnlyList<DealtCard> DealtCards,
    IReadOnlyList<Card> BurnCards,
    int? ReplaySeed);

/// <summary>
/// Service interface for dealing cards according to variant rules.
/// Provides high-level operations for dealing phases (hole cards, community cards, etc.)
/// and enforces card visibility rules.
/// </summary>
public interface ICardDealingService
{
    /// <summary>
    /// Gets the variant identifier for this dealing service.
    /// </summary>
    string VariantId { get; }

    /// <summary>
    /// Gets the visibility provider for this dealing service.
    /// </summary>
    ICardVisibilityProvider VisibilityProvider { get; }

    /// <summary>
    /// Initializes the dealing service for a new hand.
    /// </summary>
    /// <param name="playerNames">The names of players at the table in seat order.</param>
    /// <param name="dealerPosition">The position of the dealer button.</param>
    /// <param name="seed">Optional seed for deterministic dealing.</param>
    void Initialize(IReadOnlyList<string> playerNames, int dealerPosition, int? seed = null);

    /// <summary>
    /// Shuffles the deck and prepares for dealing.
    /// </summary>
    void Shuffle();

    /// <summary>
    /// Deals hole cards to all players according to variant rules.
    /// </summary>
    /// <returns>The result of the dealing phase.</returns>
    DealingPhaseResult DealHoleCards();

    /// <summary>
    /// Deals community cards for a specific street.
    /// </summary>
    /// <param name="streetName">The name of the street (e.g., "Flop", "Turn", "River").</param>
    /// <returns>The result of the dealing phase.</returns>
    DealingPhaseResult DealCommunityCards(string streetName);

    /// <summary>
    /// Deals a single card to a player (for stud games or manual dealing).
    /// </summary>
    /// <param name="playerName">The name of the player to deal to.</param>
    /// <param name="faceUp">Whether the card is dealt face up.</param>
    /// <returns>The dealt card with visibility information.</returns>
    DealtCard DealToPlayer(string playerName, bool faceUp);

    /// <summary>
    /// Gets the cards visible to a specific viewer.
    /// </summary>
    /// <param name="allDealtCards">All cards that have been dealt.</param>
    /// <param name="viewer">The player viewing the cards.</param>
    /// <returns>Cards visible to the viewer (with hidden cards represented as null).</returns>
    IReadOnlyList<(DealtCard DealtCard, Card VisibleCard)> GetVisibleCards(IReadOnlyList<DealtCard> allDealtCards, string viewer);

    /// <summary>
    /// Gets the burn cards that have been dealt.
    /// </summary>
    IReadOnlyList<Card> BurnCards { get; }

    /// <summary>
    /// Gets the current seed used for dealing.
    /// </summary>
    int Seed { get; }

    /// <summary>
    /// Gets the number of cards remaining in the deck.
    /// </summary>
    int CardsRemaining { get; }
}

/// <summary>
/// Default implementation of ICardDealingService.
/// Wraps an IDealingEngine and adds visibility rules.
/// </summary>
public class CardDealingService : ICardDealingService
{
    private readonly IDealingEngine _dealingEngine;
    private readonly ICardVisibilityProvider _visibilityProvider;
    private readonly List<Card> _burnCards = [];
    private IReadOnlyList<string> _playerNames = Array.Empty<string>();

    /// <summary>
    /// Creates a new card dealing service.
    /// </summary>
    /// <param name="dealingEngine">The dealing engine to use.</param>
    /// <param name="visibilityProvider">The visibility provider to use.</param>
    public CardDealingService(IDealingEngine dealingEngine, ICardVisibilityProvider visibilityProvider)
    {
        _dealingEngine = dealingEngine ?? throw new ArgumentNullException(nameof(dealingEngine));
        _visibilityProvider = visibilityProvider ?? throw new ArgumentNullException(nameof(visibilityProvider));
    }

    /// <inheritdoc/>
    public string VariantId => _dealingEngine.VariantId;

    /// <inheritdoc/>
    public ICardVisibilityProvider VisibilityProvider => _visibilityProvider;

    /// <inheritdoc/>
    public IReadOnlyList<Card> BurnCards => _burnCards.AsReadOnly();

    /// <inheritdoc/>
    public int Seed => _dealingEngine.Seed;

    /// <inheritdoc/>
    public int CardsRemaining => _dealingEngine.CardsRemaining;

    /// <inheritdoc/>
    public void Initialize(IReadOnlyList<string> playerNames, int dealerPosition, int? seed = null)
    {
        _playerNames = playerNames;
        _burnCards.Clear();
        _dealingEngine.Initialize(playerNames, dealerPosition, seed);
    }

    /// <inheritdoc/>
    public void Shuffle()
    {
        _burnCards.Clear();
        _dealingEngine.Shuffle();
    }

    /// <inheritdoc/>
    public DealingPhaseResult DealHoleCards()
    {
        var results = _dealingEngine.DealHoleCards();
        var dealtCards = new List<DealtCard>();

        foreach (var result in results)
        {
            var isFaceUp = _visibilityProvider.IsFaceUp(result.CardType);
            dealtCards.Add(new DealtCard(
                result.Card,
                result.Recipient,
                result.CardType,
                result.DealSequence,
                isFaceUp));
        }

        return new DealingPhaseResult(
            "Hole Cards",
            dealtCards,
            _burnCards.ToList(),
            Seed);
    }

    /// <inheritdoc/>
    public DealingPhaseResult DealCommunityCards(string streetName)
    {
        var results = _dealingEngine.DealCommunityCards(streetName);
        var dealtCards = new List<DealtCard>();

        foreach (var result in results)
        {
            var isFaceUp = _visibilityProvider.IsFaceUp(result.CardType);
            var dealtCard = new DealtCard(
                result.Card,
                result.Recipient,
                result.CardType,
                result.DealSequence,
                isFaceUp);

            if (result.CardType == DealCardType.BurnCard)
            {
                _burnCards.Add(result.Card);
            }

            dealtCards.Add(dealtCard);
        }

        return new DealingPhaseResult(
            streetName,
            dealtCards,
            _burnCards.ToList(),
            Seed);
    }

    /// <inheritdoc/>
    public DealtCard DealToPlayer(string playerName, bool faceUp)
    {
        var result = _dealingEngine.DealToPlayer(playerName, faceUp);
        var isFaceUp = _visibilityProvider.IsFaceUp(result.CardType);

        return new DealtCard(
            result.Card,
            result.Recipient,
            result.CardType,
            result.DealSequence,
            isFaceUp);
    }

    /// <inheritdoc/>
    public IReadOnlyList<(DealtCard DealtCard, Card VisibleCard)> GetVisibleCards(
        IReadOnlyList<DealtCard> allDealtCards,
        string viewer)
    {
        var result = new List<(DealtCard DealtCard, Card VisibleCard)>();

        foreach (var dealt in allDealtCards)
        {
            var canView = _visibilityProvider.CanViewCard(dealt.CardType, dealt.Recipient, viewer);
            result.Add((dealt, canView ? dealt.Card : null));
        }

        return result;
    }
}

/// <summary>
/// Factory for creating card dealing services based on variant.
/// </summary>
public static class CardDealingServiceFactory
{
    /// <summary>
    /// Creates a card dealing service for Hold'em.
    /// </summary>
    /// <returns>A card dealing service configured for Hold'em.</returns>
    public static ICardDealingService CreateHoldEm()
    {
        var engine = new HoldEmDealingEngine();
        var visibility = new CommunityCardVisibilityProvider("holdem");
        return new CardDealingService(engine, visibility);
    }

    /// <summary>
    /// Creates a card dealing service for Omaha.
    /// </summary>
    /// <returns>A card dealing service configured for Omaha.</returns>
    public static ICardDealingService CreateOmaha()
    {
        var engine = new OmahaDealingEngine();
        var visibility = new CommunityCardVisibilityProvider("omaha");
        return new CardDealingService(engine, visibility);
    }

    /// <summary>
    /// Creates a card dealing service for Seven Card Stud.
    /// </summary>
    /// <returns>A card dealing service configured for Seven Card Stud.</returns>
    public static ICardDealingService CreateSevenCardStud()
    {
        var engine = new SevenCardStudDealingEngine();
        var visibility = new StudVisibilityProvider("seven-card-stud");
        return new CardDealingService(engine, visibility);
    }

    /// <summary>
    /// Creates a card dealing service for the specified variant.
    /// </summary>
    /// <param name="variantId">The variant identifier.</param>
    /// <returns>A card dealing service for the variant.</returns>
    public static ICardDealingService Create(string variantId)
    {
        return variantId.ToLowerInvariant() switch
        {
            "holdem" or "texas-holdem" => CreateHoldEm(),
            "omaha" or "omaha-hi" or "omaha-hilo" => CreateOmaha(),
            "seven-card-stud" or "stud" => CreateSevenCardStud(),
            _ => CreateHoldEm()
        };
    }
}
