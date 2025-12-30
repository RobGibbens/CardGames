using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Hands.DrawHands;

/// <summary>
/// A five-card draw hand for Twos, Jacks, Man with the Axe variant.
/// Wild cards: All 2s, all Jacks, and the King of Diamonds.
/// </summary>
public sealed class TwosJacksManWithTheAxeDrawHand : FiveCardHand
{
    private readonly TwosJacksManWithTheAxeWildCardRules _wildCardRules;
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

    public TwosJacksManWithTheAxeDrawHand(IReadOnlyCollection<Card> cards)
        : base(cards)
    {
        _wildCardRules = new TwosJacksManWithTheAxeWildCardRules();
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

    /// <summary>
    /// Checks if this hand contains a natural pair of sevens.
    /// A "natural" pair of sevens means exactly two 7s that are NOT wild cards.
    /// Note: 7s are never wild in this variant, so any 7s in the hand are natural.
    /// </summary>
    /// <returns>True if the hand contains at least two non-wild 7s.</returns>
    public bool HasNaturalPairOfSevens()
    {
        // Count non-wild sevens (7s are never wild in this variant, but we check explicitly for clarity)
        var naturalSevens = Cards
            .Where(c => c.Symbol == Symbol.Seven && !WildCards.Contains(c))
            .Count();

        return naturalSevens >= 2;
    }
}