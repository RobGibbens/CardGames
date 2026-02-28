using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Hand for "The Good, the Bad, and the Ugly" — a seven card stud variant
/// where cards matching "The Good" table card are wild.
/// </summary>
public class GoodBadUglyHand : StudHand
{
    private readonly GoodBadUglyWildCardRules _wildCardRules;
    private readonly int? _wildRank;
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

    public GoodBadUglyHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        IReadOnlyCollection<Card> downCards,
        int? wildRank,
        GoodBadUglyWildCardRules wildCardRules)
        : base(holeCards, openCards, downCards)
    {
        _wildRank = wildRank;
        _wildCardRules = wildCardRules;
    }

    private IReadOnlyCollection<Card> DetermineWildCards()
        => _wildCardRules.DetermineWildCards(Cards, _wildRank);

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
