using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Follow the Queen hand: a seven card stud variant where Queens are always wild,
/// and the card following the last dealt face-up Queen (and all cards of that rank) 
/// are also wild.
/// </summary>
public class FollowTheQueenHand : StudHand
{
    private readonly FollowTheQueenWildCardRules _wildCardRules;
    private readonly IReadOnlyCollection<Card> _faceUpCardsInOrder;
    private IReadOnlyCollection<Card> _wildCards;
    private HandType _evaluatedType;
    private long _evaluatedStrength;
    private IReadOnlyCollection<Card> _evaluatedBestCards;
    private bool _evaluated;

    public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();
    
    /// <summary>
    /// Gets the evaluated best 5-card hand after applying wild cards.
    /// This represents what the hand would look like if wild cards were substituted
    /// for their optimal values.
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
    /// Creates a Follow the Queen hand.
    /// </summary>
    /// <param name="holeCards">The player's hole cards (typically 2 cards).</param>
    /// <param name="openCards">The player's face-up board cards (typically 4 cards).</param>
    /// <param name="downCard">The final face-down card dealt (typically 1 card).</param>
    /// <param name="faceUpCardsInOrder">All face-up cards dealt to all players in the order they were dealt.</param>
    public FollowTheQueenHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard,
        IReadOnlyCollection<Card> faceUpCardsInOrder)
        : base(holeCards, openCards, new[] { downCard })
    {
        if (holeCards.Count != 2)
        {
            throw new ArgumentException("Follow the Queen needs exactly two hole cards", nameof(holeCards));
        }
        if (openCards.Count > 4)
        {
            throw new ArgumentException("Follow the Queen has at most four open cards", nameof(openCards));
        }

        _wildCardRules = new FollowTheQueenWildCardRules();
        _faceUpCardsInOrder = faceUpCardsInOrder;
    }

    private IReadOnlyCollection<Card> DetermineWildCards()
        => _wildCardRules.DetermineWildCards(Cards, _faceUpCardsInOrder);

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

        if (!WildCards.Any())
        {
            _evaluatedType = base.DetermineType();
            _evaluatedStrength = base.CalculateStrength();
            // Find the best 5-card hand from all possible hands
            _evaluatedBestCards = FindBestFiveCardHand();
            return;
        }

        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            Cards, WildCards, Ranking);
        _evaluatedType = type;
        _evaluatedStrength = strength;
        _evaluatedBestCards = evaluatedCards;
    }
    
    private IReadOnlyCollection<Card> FindBestFiveCardHand()
    {
        return PossibleHands()
            .Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) })
            .Where(pair => pair.type == Type)
            .OrderByDescending(pair => HandStrength.Calculate(pair.hand.ToList(), pair.type, Ranking))
            .First()
            .hand
            .ToList();
    }
}
