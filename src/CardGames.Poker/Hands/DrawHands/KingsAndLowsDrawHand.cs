using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Hands.DrawHands;

/// <summary>
/// A five-card draw hand for Kings and Lows variant.
/// Wild cards: All Kings and all cards with the lowest value in the hand (excluding Kings).
/// Aces can be treated as low (value 1) when it produces a better hand.
/// </summary>
public sealed class KingsAndLowsDrawHand : FiveCardHand
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
    /// Gets the evaluated best 5-card hand after applying wild card substitutions.
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

    protected override HandTypeStrengthRanking Ranking { get; } = HandTypeStrengthRanking.Classic;

    public KingsAndLowsDrawHand(IReadOnlyCollection<Card> cards)
        : base(cards)
    {
        // Kings and Lows: Kings are always wild, plus the lowest non-King card(s)
        // kingRequired: false means the lowest card is wild even without a King
        _wildCardRules = new WildCardRules(kingRequired: false);
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
        IReadOnlyCollection<Card> bestCards = Cards;
        IReadOnlyCollection<Card> bestWildCards = Array.Empty<Card>();

        foreach (var wildCards in wildCardSets)
        {
            HandType type;
            long strength;
            IReadOnlyCollection<Card> evaluatedCards;

            if (!wildCards.Any())
            {
                // No wild cards - use standard evaluation
                type = HandTypeDetermination.DetermineHandType(Cards);
                strength = HandStrength.Calculate(Cards.ToList(), type, Ranking);
                evaluatedCards = Cards;
            }
            else
            {
                // Use wild card evaluator for hands with wild cards
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

    protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
    {
        yield return Cards;
    }
}
