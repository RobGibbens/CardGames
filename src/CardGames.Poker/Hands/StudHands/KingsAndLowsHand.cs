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
    private bool _evaluated;

    public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();

    public KingsAndLowsHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard,
        WildCardRules wildCardRules) 
        : base(holeCards, openCards, new[] { downCard })
    {
        if (holeCards.Count != 2)
        {
            throw new ArgumentException("Kings and Lows needs exactly two hole cards");
        }
        if (openCards.Count > 4)
        {
            throw new ArgumentException("Kings and Lows has at most four open cards");
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
            return;
        }

        var (type, strength) = WildCardHandEvaluator.EvaluateBestHand(
            Cards, WildCards, Ranking);
        _evaluatedType = type;
        _evaluatedStrength = strength;
    }
}
