using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Result of a hand evaluation containing hand type, strength, winning cards, and kicker information.
/// </summary>
public sealed class HandEvaluationResult
{
    /// <summary>
    /// The type of the evaluated hand (e.g., Flush, Straight, etc.).
    /// </summary>
    public HandType Type { get; }

    /// <summary>
    /// The strength of the hand for comparison purposes.
    /// Higher values indicate stronger hands.
    /// </summary>
    public long Strength { get; }

    /// <summary>
    /// The five cards that make up the winning hand.
    /// </summary>
    public IReadOnlyCollection<Card> WinningCards { get; }

    /// <summary>
    /// The primary cards that form the hand (e.g., the pair, trips, etc.).
    /// For high card hands, this is empty.
    /// </summary>
    public IReadOnlyCollection<Card> PrimaryCards { get; }

    /// <summary>
    /// The kicker cards that break ties between same-type hands.
    /// </summary>
    public IReadOnlyCollection<Card> Kickers { get; }

    public HandEvaluationResult(
        HandType type,
        long strength,
        IReadOnlyCollection<Card> winningCards,
        IReadOnlyCollection<Card> primaryCards,
        IReadOnlyCollection<Card> kickers)
    {
        Type = type;
        Strength = strength;
        WinningCards = winningCards;
        PrimaryCards = primaryCards;
        Kickers = kickers;
    }

    /// <summary>
    /// Creates a HandEvaluationResult with automatic primary/kicker extraction.
    /// </summary>
    public static HandEvaluationResult Create(
        HandType type,
        long strength,
        IReadOnlyCollection<Card> winningCards)
    {
        var (primaryCards, kickers) = ExtractPrimaryAndKickers(type, winningCards);
        return new HandEvaluationResult(type, strength, winningCards, primaryCards, kickers);
    }

    private static (IReadOnlyCollection<Card> Primary, IReadOnlyCollection<Card> Kickers) ExtractPrimaryAndKickers(
        HandType type,
        IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 5)
        {
            return (cards, new List<Card>());
        }

        var grouped = cards
            .GroupBy(c => c.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToList();

        return type switch
        {
            HandType.FiveOfAKind => (cards, new List<Card>()),
            HandType.StraightFlush => (cards, new List<Card>()),
            HandType.Quads => ExtractQuads(grouped),
            HandType.FullHouse => ExtractFullHouse(grouped),
            HandType.Flush => (cards, new List<Card>()),
            HandType.Straight => (cards, new List<Card>()),
            HandType.Trips => ExtractTrips(grouped),
            HandType.TwoPair => ExtractTwoPair(grouped),
            HandType.OnePair => ExtractOnePair(grouped),
            HandType.HighCard => (new List<Card>(), cards.OrderByDescending(c => c.Value).ToList()),
            _ => (cards, new List<Card>())
        };
    }

    private static (IReadOnlyCollection<Card>, IReadOnlyCollection<Card>) ExtractQuads(
        List<IGrouping<int, Card>> grouped)
    {
        var quads = grouped.First(g => g.Count() == 4).ToList();
        var kickers = grouped.Where(g => g.Count() != 4).SelectMany(g => g).Take(1).ToList();
        return (quads, kickers);
    }

    private static (IReadOnlyCollection<Card>, IReadOnlyCollection<Card>) ExtractFullHouse(
        List<IGrouping<int, Card>> grouped)
    {
        var trips = grouped.First(g => g.Count() >= 3).Take(3).ToList();
        var pair = grouped.First(g => g.Key != trips.First().Value && g.Count() >= 2).Take(2).ToList();
        return (trips.Concat(pair).ToList(), new List<Card>());
    }

    private static (IReadOnlyCollection<Card>, IReadOnlyCollection<Card>) ExtractTrips(
        List<IGrouping<int, Card>> grouped)
    {
        var trips = grouped.First(g => g.Count() == 3).ToList();
        var kickers = grouped.Where(g => g.Count() != 3).SelectMany(g => g)
            .OrderByDescending(c => c.Value).Take(2).ToList();
        return (trips, kickers);
    }

    private static (IReadOnlyCollection<Card>, IReadOnlyCollection<Card>) ExtractTwoPair(
        List<IGrouping<int, Card>> grouped)
    {
        var pairs = grouped.Where(g => g.Count() == 2).Take(2).SelectMany(g => g).ToList();
        var kickers = grouped.SelectMany(g => g)
            .Where(c => !pairs.Contains(c))
            .OrderByDescending(c => c.Value).Take(1).ToList();
        return (pairs, kickers);
    }

    private static (IReadOnlyCollection<Card>, IReadOnlyCollection<Card>) ExtractOnePair(
        List<IGrouping<int, Card>> grouped)
    {
        var pair = grouped.First(g => g.Count() == 2).ToList();
        var kickers = grouped.SelectMany(g => g)
            .Where(c => !pair.Contains(c))
            .OrderByDescending(c => c.Value).Take(3).ToList();
        return (pair, kickers);
    }

    public override string ToString()
        => $"{Type} [{string.Join(" ", WinningCards)}]";
}
