using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

public class KingsAndLowsHand : StudHand
{
    private readonly WildCardRules _wildCardRules;
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

    public KingsAndLowsHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard,
        WildCardRules wildCardRules) 
        : base(holeCards, openCards, new[] { downCard })
    {
        if (holeCards.Count != 2)
        {
            throw new ArgumentException("Kings and Lows needs exactly two hole cards", nameof(holeCards));
        }
        if (openCards.Count > 4)
        {
            throw new ArgumentException("Kings and Lows has at most four open cards", nameof(openCards));
        }

        _wildCardRules = wildCardRules;
    }

    private IReadOnlyCollection<Card> DetermineWildCards()
        => _wildCardRules.DetermineWildCards(Cards);

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
