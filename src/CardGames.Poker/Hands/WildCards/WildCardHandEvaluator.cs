using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.WildCards;

public static class WildCardHandEvaluator
{
    private static readonly IReadOnlyCollection<Card> AllCards = Enumerable
        .Range(2, 13)
        .SelectMany(v => new[] { Suit.Hearts, Suit.Diamonds, Suit.Spades, Suit.Clubs }
            .Select(s => new Card(s, v)))
        .ToList();

    public static (HandType Type, long Strength, IReadOnlyCollection<Card> EvaluatedCards, IReadOnlyCollection<Card> SourceCards) EvaluateBestHand(
        IReadOnlyCollection<Card> allCards,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        var fiveCardHands = allCards.Count < 5 
            ? new[] { allCards } 
            : allCards.SubsetsOfSize(5);

        HandType bestType = HandType.Incomplete;
        long bestStrength = 0;
        IReadOnlyCollection<Card> bestCards = new List<Card>();
        IReadOnlyCollection<Card> bestSourceCards = new List<Card>();

        foreach (var hand in fiveCardHands)
        {
            var handList = hand.ToList();
            var wildInHand = handList.Where(c => wildCards.Contains(c)).ToList();

            HandType type;
            long strength;
            IReadOnlyCollection<Card> evaluatedCards;

            if (wildInHand.Any())
            {
                (type, strength, evaluatedCards) = EvaluateWithWildCards(handList, wildInHand, ranking);
            }
            else
            {
                type = HandTypeDetermination.DetermineHandType(handList);
                strength = HandStrength.Calculate(handList, type, ranking);
                evaluatedCards = handList;
            }

            if (strength > bestStrength)
            {
                bestType = type;
                bestStrength = strength;
                bestCards = evaluatedCards;
                bestSourceCards = handList;
            }
        }

        return (bestType, bestStrength, bestCards, bestSourceCards);
    }

    private static (HandType Type, long Strength, IReadOnlyCollection<Card> EvaluatedCards) EvaluateWithWildCards(
        IReadOnlyCollection<Card> hand,
        IReadOnlyCollection<Card> wildCards,
        HandTypeStrengthRanking ranking)
    {
        var naturalCards = hand.Except(wildCards).ToList();
        var wildCount = wildCards.Count;

        if (wildCount == 5)
        {
            var fiveAces = Enumerable.Repeat(new Card(Suit.Spades, 14), 5).ToList();
            return (HandType.FiveOfAKind, HandStrength.Calculate(
                fiveAces,
                HandType.FiveOfAKind,
                ranking), fiveAces);
        }

        var result = TryMakeFiveOfAKind(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeStraightFlush(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeQuads(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeFullHouse(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeFlush(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeStraight(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeTrips(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakeTwoPair(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        result = TryMakePair(naturalCards, wildCount, ranking);
        if (result.HasValue) return result.Value;

        return MakeHighCard(naturalCards, wildCount, ranking);
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeFiveOfAKind(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        if (naturalCards.Count == 0) return null;

        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Value)
            .ToList();

        var bestValue = valueCounts.First();
        if (bestValue.Count + wildCount >= 5)
        {
            var cards = Enumerable.Repeat(new Card(Suit.Spades, bestValue.Value), 5).ToList();
            return (HandType.FiveOfAKind, HandStrength.Calculate(cards, HandType.FiveOfAKind, ranking), cards);
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeStraightFlush(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        foreach (var suit in new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs })
        {
            var suitCards = naturalCards.Where(c => c.Suit == suit).ToList();

            // Check high straights (A-high down to 6-high)
            for (int highValue = 14; highValue >= 6; highValue--)
            {
                var needed = Enumerable.Range(highValue - 4, 5).ToList();
                var present = suitCards.Count(c => needed.Contains(c.Value));
                if (present + wildCount >= 5)
                {
                    var cards = needed.Select(v => new Card(suit, v)).ToList();
                    return (HandType.StraightFlush, HandStrength.Calculate(cards, HandType.StraightFlush, ranking), cards);
                }
            }

            // Check wheel (A-2-3-4-5) - Ace counts as 1
            var wheelValues = new[] { 14, 2, 3, 4, 5 }; // Ace represented as 14 in the card, but used as 1
            var wheelPresent = suitCards.Count(c => wheelValues.Contains(c.Value));
            if (wheelPresent + wildCount >= 5)
            {
                // For wheel, we use values 1-5 to represent A-2-3-4-5 for strength calculation
                // but the cards themselves still have Ace as 14
                var cards = new List<Card>
                {
                    new Card(suit, 5),
                    new Card(suit, 4),
                    new Card(suit, 3),
                    new Card(suit, 2),
                    new Card(suit, 14) // Ace
                };
                return (HandType.StraightFlush, HandStrength.Calculate(cards, HandType.StraightFlush, ranking), cards);
            }
        }
        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeQuads(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Value)
            .ToList();

        foreach (var vc in valueCounts)
        {
            if (vc.Count + wildCount >= 4)
            {
                var wildsUsed = 4 - vc.Count;
                var remainingWild = wildCount - wildsUsed;
                var kicker = remainingWild > 0 ? 14 : naturalCards.Where(c => c.Value != vc.Value).Max(c => (int?)c.Value) ?? 2;
                var cards = new List<Card>
                {
                    new Card(Suit.Spades, vc.Value),
                    new Card(Suit.Hearts, vc.Value),
                    new Card(Suit.Diamonds, vc.Value),
                    new Card(Suit.Clubs, vc.Value),
                    new Card(Suit.Spades, kicker)
                };
                return (HandType.Quads, HandStrength.Calculate(cards, HandType.Quads, ranking), cards);
            }
        }

        if (wildCount >= 4)
        {
            var highestNatural = naturalCards.Max(c => c.Value);
            var cards = new List<Card>
            {
                new Card(Suit.Spades, 14),
                new Card(Suit.Hearts, 14),
                new Card(Suit.Diamonds, 14),
                new Card(Suit.Clubs, 14),
                new Card(Suit.Spades, highestNatural)
            };
            return (HandType.Quads, HandStrength.Calculate(cards, HandType.Quads, ranking), cards);
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeFullHouse(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Value)
            .ToList();

        for (int i = 0; i < valueCounts.Count; i++)
        {
            for (int j = 0; j < valueCounts.Count; j++)
            {
                if (i == j) continue;
                var tripsValue = valueCounts[i];
                var pairValue = valueCounts[j];
                
                var tripsNeeded = System.Math.Max(0, 3 - tripsValue.Count);
                var pairNeeded = System.Math.Max(0, 2 - pairValue.Count);
                
                if (tripsNeeded + pairNeeded <= wildCount)
                {
                    var cards = new List<Card>
                    {
                        new Card(Suit.Spades, tripsValue.Value),
                        new Card(Suit.Hearts, tripsValue.Value),
                        new Card(Suit.Diamonds, tripsValue.Value),
                        new Card(Suit.Spades, pairValue.Value),
                        new Card(Suit.Hearts, pairValue.Value)
                    };
                    return (HandType.FullHouse, HandStrength.Calculate(cards, HandType.FullHouse, ranking), cards);
                }
            }
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeFlush(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        foreach (var suit in new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs })
        {
            var suitCards = naturalCards.Where(c => c.Suit == suit).OrderByDescending(c => c.Value).ToList();
            if (suitCards.Count + wildCount >= 5)
            {
                var flushCards = suitCards.Take(5 - wildCount).ToList();
                for (int v = 14; flushCards.Count < 5; v--)
                {
                    if (!flushCards.Any(c => c.Value == v))
                    {
                        flushCards.Add(new Card(suit, v));
                    }
                }
                flushCards = flushCards.OrderByDescending(c => c.Value).ToList();
                return (HandType.Flush, HandStrength.Calculate(flushCards, HandType.Flush, ranking), flushCards);
            }
        }
        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeStraight(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var distinctValues = naturalCards.Select(c => c.Value).Distinct().ToHashSet();

        // Check high straights (A-high down to 6-high)
        for (int highValue = 14; highValue >= 6; highValue--)
        {
            var needed = Enumerable.Range(highValue - 4, 5).ToList();
            var present = needed.Count(v => distinctValues.Contains(v));
            if (present + wildCount >= 5)
            {
                // Use alternating suits to avoid creating a flush
                var suits = new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
                var cards = needed.Select((v, i) => new Card(suits[i % 2], v)).ToList();
                return (HandType.Straight, HandStrength.Calculate(cards, HandType.Straight, ranking), cards);
            }
        }

        // Check wheel (A-2-3-4-5) - Ace counts as 1
        var wheelValues = new[] { 14, 2, 3, 4, 5 }; // Ace represented as 14 in the card, but used as 1
        var wheelPresent = wheelValues.Count(v => distinctValues.Contains(v));
        if (wheelPresent + wildCount >= 5)
        {
            // Use alternating suits to avoid creating a flush
            var suits = new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
            var cards = new List<Card>
            {
                new Card(suits[0], 5),
                new Card(suits[1], 4),
                new Card(suits[0], 3),
                new Card(suits[1], 2),
                new Card(suits[0], 14) // Ace
            };
            return (HandType.Straight, HandStrength.Calculate(cards, HandType.Straight, ranking), cards);
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeTrips(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Value)
            .ToList();

        foreach (var vc in valueCounts)
        {
            if (vc.Count + wildCount >= 3)
            {
                var wildsUsed = System.Math.Max(0, 3 - vc.Count);
                var remainingWild = wildCount - wildsUsed;
                var kickers = naturalCards
                    .Where(c => c.Value != vc.Value)
                    .OrderByDescending(c => c.Value)
                    .Take(2 - remainingWild)
                    .Select(c => c.Value)
                    .ToList();
                while (kickers.Count < 2)
                {
                    for (int v = 14; kickers.Count < 2; v--)
                    {
                        if (v != vc.Value && !kickers.Contains(v))
                        {
                            kickers.Add(v);
                        }
                    }
                }
                var cards = new List<Card>
                {
                    new Card(Suit.Spades, vc.Value),
                    new Card(Suit.Hearts, vc.Value),
                    new Card(Suit.Diamonds, vc.Value),
                    new Card(Suit.Spades, kickers[0]),
                    new Card(Suit.Hearts, kickers[1])
                };
                return (HandType.Trips, HandStrength.Calculate(cards, HandType.Trips, ranking), cards);
            }
        }
        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakeTwoPair(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Value)
            .ToList();

        var pairs = valueCounts.Where(vc => vc.Count >= 2).Take(2).ToList();
        if (pairs.Count >= 2)
        {
            var kicker = naturalCards.Where(c => c.Value != pairs[0].Value && c.Value != pairs[1].Value)
                .OrderByDescending(c => c.Value)
                .FirstOrDefault()?.Value ?? 14;
            var cards = new List<Card>
            {
                new Card(Suit.Spades, pairs[0].Value),
                new Card(Suit.Hearts, pairs[0].Value),
                new Card(Suit.Spades, pairs[1].Value),
                new Card(Suit.Hearts, pairs[1].Value),
                new Card(Suit.Spades, kicker)
            };
            return (HandType.TwoPair, HandStrength.Calculate(cards, HandType.TwoPair, ranking), cards);
        }

        if (pairs.Count == 1 && wildCount >= 1)
        {
            var secondPairValue = naturalCards
                .Where(c => c.Value != pairs[0].Value)
                .OrderByDescending(c => c.Value)
                .FirstOrDefault()?.Value ?? (pairs[0].Value == 14 ? 13 : 14);
            var kicker = naturalCards
                .Where(c => c.Value != pairs[0].Value && c.Value != secondPairValue)
                .OrderByDescending(c => c.Value)
                .FirstOrDefault()?.Value ?? 14;
            var cards = new List<Card>
            {
                new Card(Suit.Spades, pairs[0].Value),
                new Card(Suit.Hearts, pairs[0].Value),
                new Card(Suit.Spades, secondPairValue),
                new Card(Suit.Hearts, secondPairValue),
                new Card(Suit.Spades, kicker)
            };
            return (HandType.TwoPair, HandStrength.Calculate(cards, HandType.TwoPair, ranking), cards);
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>)? TryMakePair(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        if (wildCount >= 1)
        {
            var highCard = naturalCards.OrderByDescending(c => c.Value).First();
            var kickers = naturalCards
                .Where(c => c.Value != highCard.Value)
                .OrderByDescending(c => c.Value)
                .Take(3)
                .Select(c => c.Value)
                .ToList();
            while (kickers.Count < 3)
            {
                for (int v = 14; kickers.Count < 3; v--)
                {
                    if (v != highCard.Value && !kickers.Contains(v))
                    {
                        kickers.Add(v);
                    }
                }
            }
            var cards = new List<Card>
            {
                new Card(Suit.Spades, highCard.Value),
                new Card(Suit.Hearts, highCard.Value),
                new Card(Suit.Spades, kickers[0]),
                new Card(Suit.Hearts, kickers[1]),
                new Card(Suit.Diamonds, kickers[2])
            };
            return (HandType.OnePair, HandStrength.Calculate(cards, HandType.OnePair, ranking), cards);
        }

        var valueCounts = naturalCards
            .GroupBy(c => c.Value)
            .Where(g => g.Count() >= 2)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();

        if (valueCounts != null)
        {
            var kickers = naturalCards
                .Where(c => c.Value != valueCounts.Value)
                .OrderByDescending(c => c.Value)
                .Take(3)
                .Select(c => c.Value)
                .ToList();
            var cards = new List<Card>
            {
                new Card(Suit.Spades, valueCounts.Value),
                new Card(Suit.Hearts, valueCounts.Value),
                new Card(Suit.Spades, kickers[0]),
                new Card(Suit.Hearts, kickers[1]),
                new Card(Suit.Diamonds, kickers[2])
            };
            return (HandType.OnePair, HandStrength.Calculate(cards, HandType.OnePair, ranking), cards);
        }

        return null;
    }

    private static (HandType, long, IReadOnlyCollection<Card>) MakeHighCard(
        IReadOnlyCollection<Card> naturalCards,
        int wildCount,
        HandTypeStrengthRanking ranking)
    {
        var sortedNatural = naturalCards.OrderByDescending(c => c.Value).ToList();
        var cards = sortedNatural.Take(5 - wildCount).ToList();
        
        for (int v = 14; cards.Count < 5; v--)
        {
            if (!cards.Any(c => c.Value == v))
            {
                cards.Add(new Card(Suit.Spades, v));
            }
        }
        
        cards = cards.OrderByDescending(c => c.Value).Take(5).ToList();
        return (HandType.HighCard, HandStrength.Calculate(cards, HandType.HighCard, ranking), cards);
    }
}
