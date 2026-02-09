using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.HandTypes;

public static class HandTypeDetermination
{
    /// <summary>
    /// The values that form a wheel straight (A-2-3-4-5).
    /// </summary>
    public static readonly int[] WheelValues = { 14, 5, 4, 3, 2 };

    public static HandType DetermineHandType(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 5)
        {
            return DeterminePartialHandType(cards);
        }

        var numberOfDistinctValues = cards.DistinctValues().Count;
        
        if (numberOfDistinctValues == 1)
        {
            return HandType.FiveOfAKind;
        }

        return numberOfDistinctValues == 5
            ? HandTypeOfDistinctValueHand(cards)
            : HandTypeOfDuplicateValueHand(cards, numberOfDistinctValues);
    }

    /// <summary>
    /// Checks if the given card values form a wheel straight (A-2-3-4-5).
    /// </summary>
    public static bool IsWheelStraight(IReadOnlyCollection<int> values)
        => values.Count == 5 && WheelValues.All(v => values.Contains(v));

    private static HandType DeterminePartialHandType(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count == 0)
        {
            return HandType.Incomplete;
        }

        var groups = cards.GroupBy(c => c.Value).ToList();
        var maxGroupSize = groups.Max(g => g.Count());

        if (maxGroupSize == 4)
        {
            return HandType.Quads;
        }

        if (maxGroupSize == 3)
        {
            return HandType.Trips;
        }

        if (maxGroupSize == 2)
        {
            var pairCount = groups.Count(g => g.Count() == 2);
            return pairCount >= 2 ? HandType.TwoPair : HandType.OnePair;
        }

        return HandType.HighCard;
    }

    private  static HandType HandTypeOfDuplicateValueHand(IReadOnlyCollection<Card> cards, int numberOfDistinctValues)
         => numberOfDistinctValues switch
         {
             4 => HandType.OnePair,
             3 => cards.ValueOfBiggestTrips() > 1
                     ? HandType.Trips
                     : HandType.TwoPair,
             2 => cards.Count(card => card.Value != cards.ValueOfBiggestTrips()) == 2
                     ? HandType.FullHouse
                     : HandType.Quads,
             _ => throw new ArgumentException()
         };

    private static HandType HandTypeOfDistinctValueHand(IReadOnlyCollection<Card> cards)
    {
        var isStraight = IsStraight(cards);
        var isFlush = IsFlush(cards);

        return isStraight && isFlush
            ? HandType.StraightFlush
            : isStraight
                ? HandType.Straight
                : isFlush
                    ? HandType.Flush
                    : HandType.HighCard;
    }

    private static bool IsFlush(IReadOnlyCollection<Card> cards) 
        => cards.DistinctSuits().Count == 1;

    private static bool IsStraight(IReadOnlyCollection<Card> cards)
    {
        var values = cards.DistinctDescendingValues();
        if (values.Count != 5)
        {
            return false;
        }

        // Check for regular straight (e.g., 5-6-7-8-9)
        if (values.Max() - values.Min() == 4)
        {
            return true;
        }

        // Check for wheel straight (A-2-3-4-5) where Ace (value 14) acts as low
        return IsWheelStraight(values);
    }
}
