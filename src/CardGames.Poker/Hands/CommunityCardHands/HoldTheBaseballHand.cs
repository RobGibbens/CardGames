using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.CommunityCardHands;

/// <summary>
/// Hold the Baseball hand: a Texas Hold 'Em variant where all 3s and 9s are wild,
/// including community cards. Players use 2 hole cards and 5 community cards.
/// </summary>
public sealed class HoldTheBaseballHand : CommunityCardsHand
{
    private readonly BaseballWildCardRules _wildCardRules;
    private IReadOnlyCollection<Card> _wildCards;
    private HandType _evaluatedType;
    private long _evaluatedStrength;
    private IReadOnlyCollection<Card> _evaluatedBestCards;
    private IReadOnlyCollection<Card> _bestHandSourceCards;
    private bool _evaluated;

    public IReadOnlyCollection<Card> WildCards => _wildCards ??= DetermineWildCards();

    /// <summary>
    /// Gets the evaluated best 5-card hand after applying wild cards.
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

    public HoldTheBaseballHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
        : base(0, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
    {
        _wildCardRules = new BaseballWildCardRules();
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
            // No wild cards — fall back to standard community card evaluation
            _evaluatedType = base.DetermineType();
            _evaluatedStrength = base.CalculateStrength();
            _evaluatedBestCards = FindBestFiveCardHand();
            _bestHandSourceCards = _evaluatedBestCards;
            return;
        }

        var (type, strength, evaluatedCards, sourceCards) = WildCardHandEvaluator.EvaluateBestHand(
            Cards, WildCards, Ranking);
        _evaluatedType = type;
        _evaluatedStrength = strength;
        _evaluatedBestCards = evaluatedCards;
        _bestHandSourceCards = sourceCards;
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
