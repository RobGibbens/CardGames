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
    /// Gets the wild cards in this hand.
    /// </summary>
    public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();

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
            // No wild cards - use standard evaluation
            _evaluatedType = HandTypeDetermination.DetermineHandType(Cards);
            _evaluatedStrength = HandStrength.Calculate(Cards.ToList(), _evaluatedType, Ranking);
            _evaluatedBestCards = Cards;
            return;
        }

        // Use wild card evaluator for hands with wild cards
        var (type, strength, evaluatedCards) = WildCardHandEvaluator.EvaluateBestHand(
            Cards, WildCards, Ranking);
        _evaluatedType = type;
        _evaluatedStrength = strength;
        _evaluatedBestCards = evaluatedCards;
    }

    protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
    {
        yield return Cards;
    }
}
