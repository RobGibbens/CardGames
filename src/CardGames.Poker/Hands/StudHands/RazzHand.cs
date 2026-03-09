using System.Collections.Generic;
using System.Linq;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Represents a Razz hand (Ace-to-Five lowball from seven-card stud structure).
/// Straights and flushes are ignored, and lower ranks are better.
/// </summary>
public sealed class RazzHand : HandBase
{
    protected override HandTypeStrengthRanking Ranking => HandTypeStrengthRanking.Classic;

    public IReadOnlyCollection<Card> HoleCards { get; }
    public IReadOnlyCollection<Card> OpenCards { get; }
    public IReadOnlyCollection<Card> DownCards { get; }

    public RazzHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        IReadOnlyCollection<Card> downCards)
        : base(holeCards.Concat(openCards).Concat(downCards).ToList())
    {
        HoleCards = holeCards;
        OpenCards = openCards;
        DownCards = downCards;
    }

    protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
    {
        if (Cards.Count < 5)
        {
            return [Cards];
        }

        return Cards
            .SubsetsOfSize(5)
            .Select(cards => cards.ToList());
    }

    public IReadOnlyList<Card> GetBestLowHand()
    {
        return PossibleHands()
            .Select(cards => cards.ToList())
            .OrderByDescending(GetRazzStrength)
            .First();
    }

    public static string GetLowHandDescription(IReadOnlyCollection<Card> cards)
    {
        var ordered = cards
            .Select(MapAceToLowValue)
            .OrderByDescending(v => v)
            .ToList();

        return string.Join("-", ordered) + " low";
    }

    protected override HandType DetermineType()
    {
        var best = GetBestLowHand();

        var groups = best.GroupBy(c => MapAceToLowValue(c)).ToList();
        var groupSizes = groups.Select(g => g.Count()).OrderByDescending(c => c).ToList();

        return groupSizes[0] switch
        {
            4 => HandType.Quads,
            3 when groupSizes.Count > 1 && groupSizes[1] == 2 => HandType.FullHouse,
            3 => HandType.Trips,
            2 when groupSizes.Count(g => g == 2) >= 2 => HandType.TwoPair,
            2 => HandType.OnePair,
            _ => HandType.HighCard
        };
    }

    protected override long CalculateStrength()
    {
        return PossibleHands().Max(GetRazzStrength);
    }

    private static long GetRazzStrength(IReadOnlyCollection<Card> cards)
    {
        var mappedValues = cards
            .Select(MapAceToLowValue)
            .OrderBy(v => v)
            .ToList();

        var groups = mappedValues
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToList();

        var groupSizes = groups
            .Select(g => g.Count())
            .Concat(Enumerable.Repeat(1, 5))
            .Take(5)
            .ToList();

        var duplicatesScore = EncodeBaseN(
            groupSizes.Select(size => 6 - size),
            6);

        var highToLow = mappedValues.OrderByDescending(v => v);
        var valueScore = EncodeBaseN(
            highToLow.Select(v => 15 - v),
            15);

        return (duplicatesScore * 1_000_000L) + valueScore;
    }

    private static long EncodeBaseN(IEnumerable<int> values, int @base)
    {
        long result = 0;
        foreach (var value in values)
        {
            result = (result * @base) + value;
        }

        return result;
    }

    private static int MapAceToLowValue(Card card)
    {
        return card.Value == 14 ? 1 : card.Value;
    }
}
