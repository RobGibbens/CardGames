using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.HandTypes;

public static class HandTypeDetermination
{
    public static HandType DetermineHandType(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 5)
        {
            return HandType.Incomplete;
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
        var wheelValues = new[] { 14, 5, 4, 3, 2 };
        return wheelValues.All(v => values.Contains(v));
    }
}
