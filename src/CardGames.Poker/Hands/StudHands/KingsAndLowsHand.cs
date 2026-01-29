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

    /// <summary>
    /// Gets the wild cards in this hand (using the optimal Ace evaluation).
    /// </summary>
    public IReadOnlyCollection<Card> WildCards
    {
        get
        {
            EvaluateIfNeeded();
            return _wildCards;
        }
    }

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

        // Get all possible wild card sets (Ace-high and Ace-low if applicable)
        var wildCardSets = _wildCardRules.GetAllPossibleWildCardSets(Cards);

        HandType bestType = HandType.Incomplete;
        long bestStrength = 0;
        IReadOnlyCollection<Card> bestCards = Cards.Take(5).ToList();
        IReadOnlyCollection<Card> bestWildCards = Array.Empty<Card>();

        foreach (var wildCards in wildCardSets)
        {
            HandType type;
            long strength;
            IReadOnlyCollection<Card> evaluatedCards;

            if (!wildCards.Any())
            {
                // Calculate type and strength directly without going through virtual properties
                // to avoid circular dependency during evaluation
                var handsAndTypes = PossibleHands()
                    .Select(hand => new { hand, handType = HandTypeDetermination.DetermineHandType(hand) })
                    .ToList();

                type = HandStrength.GetEffectiveType(handsAndTypes.Select(pair => pair.handType), Ranking);
                strength = handsAndTypes
                    .Where(pair => pair.handType == type)
                    .Select(pair => HandStrength.Calculate(pair.hand.ToList(), type, Ranking))
                    .Max();
                evaluatedCards = handsAndTypes
                    .Where(pair => pair.handType == type)
                    .OrderByDescending(pair => HandStrength.Calculate(pair.hand.ToList(), type, Ranking))
                    .First()
                    .hand
                    .ToList();
            }
            else
            {
                (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
                    Cards, wildCards, Ranking);
            }

            if (strength > bestStrength)
            {
                bestType = type;
                bestStrength = strength;
                bestCards = evaluatedCards;
                bestWildCards = wildCards;
            }
        }

        _evaluatedType = bestType;
        _evaluatedStrength = bestStrength;
        _evaluatedBestCards = bestCards;
        _wildCards = bestWildCards;
    }
}
