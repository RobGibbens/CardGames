using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Hands.CommunityCardHands;

/// <summary>
/// Klondike Hold'em hand: a Hold'em-style hand where one specific community card
/// (the Klondike Card) acts as a wild card. Each player independently assigns
/// any rank and suit to the Klondike Card to make their best 5-card hand.
/// </summary>
public sealed class KlondikeHand : CommunityCardsHand
{
    private readonly Card _klondikeCard;
    private HandType _evaluatedType;
    private long _evaluatedStrength;
    private IReadOnlyCollection<Card> _evaluatedBestCards;
    private IReadOnlyCollection<Card> _bestHandSourceCards;
    private bool _evaluated;

    /// <summary>
    /// Gets the evaluated best 5-card hand after applying the Klondike wild card.
    /// </summary>
    public IReadOnlyCollection<Card> EvaluatedBestCards
    {
        get
        {
            EvaluateIfNeeded();
            return _evaluatedBestCards;
        }
    }

    /// <summary>
    /// Gets the original cards from the player's hand that formed the best evaluated hand.
    /// </summary>
    public IReadOnlyCollection<Card> BestHandSourceCards
    {
        get
        {
            EvaluateIfNeeded();
            return _bestHandSourceCards;
        }
    }

    public KlondikeHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> communityCards,
        Card klondikeCard)
        : base(0, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
    {
        _klondikeCard = klondikeCard;
    }

    protected override long CalculateStrength()
    {
        EvaluateIfNeeded();
        return _evaluatedStrength;
    }

    protected override HandType DetermineType()
    {
        EvaluateIfNeeded();
        return _evaluatedType;
    }

    private void EvaluateIfNeeded()
    {
        if (_evaluated)
        {
            return;
        }

        _evaluated = true;

        // The Klondike Card is always wild — treat it as a single-card wild set
        var wildCards = new List<Card> { _klondikeCard };
        var allCards = HoleCards.Concat(CommunityCards).ToList();

        var (type, strength, evaluatedCards, sourceCards) = WildCardHandEvaluator.EvaluateBestHand(
            allCards, wildCards, Ranking);

        _evaluatedType = type;
        _evaluatedStrength = strength;
        _evaluatedBestCards = evaluatedCards;
        _bestHandSourceCards = sourceCards;
    }
}
