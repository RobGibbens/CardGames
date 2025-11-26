using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Formats hand descriptions for display in the CLI.
/// Provides human-readable descriptions like "Four of a Kind (Aces)" or "Flush (Hearts)".
/// </summary>
internal static class HandDescriptionFormatter
{
    /// <summary>
    /// Gets a human-readable description of the hand type with relevant details.
    /// </summary>
    public static string GetHandDescription(HandBase hand)
    {
        var cards = hand.Cards.ToList();
        var handType = hand.Type;

        return handType switch
        {
            HandType.HighCard => FormatHighCard(cards),
            HandType.OnePair => FormatOnePair(cards),
            HandType.TwoPair => FormatTwoPair(cards),
            HandType.Trips => FormatTrips(cards),
            HandType.Straight => FormatStraight(cards),
            HandType.Flush => FormatFlush(cards),
            HandType.FullHouse => FormatFullHouse(cards),
            HandType.Quads => FormatQuads(cards),
            HandType.StraightFlush => FormatStraightFlush(cards),
            HandType.FiveOfAKind => FormatFiveOfAKind(cards),
            _ => "Incomplete Hand"
        };
    }

    private static string FormatHighCard(IReadOnlyCollection<Card> cards)
    {
        var highCard = cards.OrderByDescending(c => c.Value).First();
        return $"High Card ({GetSymbolName(highCard.Symbol)})";
    }

    private static string FormatOnePair(IReadOnlyCollection<Card> cards)
    {
        var pairValue = cards.ValueOfBiggestPair();
        if (pairValue == 0)
        {
            // Wild card hand - pair made with wild cards
            var highCard = cards.OrderByDescending(c => c.Value).First();
            return $"Pair of {GetPluralSymbolName(highCard.Symbol)}";
        }
        var symbol = pairValue.ToSymbol();
        return $"Pair of {GetPluralSymbolName(symbol)}";
    }

    private static string FormatTwoPair(IReadOnlyCollection<Card> cards)
    {
        var pairs = cards
            .GroupBy(c => c.Value)
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Key)
            .Take(2)
            .Select(g => g.Key.ToSymbol())
            .ToList();

        if (pairs.Count >= 2)
        {
            return $"Two Pair ({GetPluralSymbolName(pairs[0])} and {GetPluralSymbolName(pairs[1])})";
        }
        return "Two Pair";
    }

    private static string FormatTrips(IReadOnlyCollection<Card> cards)
    {
        var tripsValue = cards.ValueOfBiggestTrips();
        if (tripsValue == 0)
        {
            // Wild card hand - trips made with wild cards
            var highCard = cards.OrderByDescending(c => c.Value).First();
            return $"Three of a Kind ({GetPluralSymbolName(highCard.Symbol)})";
        }
        var symbol = tripsValue.ToSymbol();
        return $"Three of a Kind ({GetPluralSymbolName(symbol)})";
    }

    private static string FormatStraight(IReadOnlyCollection<Card> cards)
    {
        var straightHighValue = FindStraightHighValue(cards);
        var symbol = straightHighValue.ToSymbol();
        return $"Straight ({GetSymbolName(symbol)}-high)";
    }

    private static int FindStraightHighValue(IReadOnlyCollection<Card> cards)
    {
        // Get distinct values in descending order
        var distinctValues = cards.DistinctDescendingValues().ToList();
        
        // Look for 5 consecutive values starting from the highest
        for (int i = 0; i <= distinctValues.Count - 5; i++)
        {
            var potentialHigh = distinctValues[i];
            var potentialLow = distinctValues[i + 4];
            
            // Check if this forms a straight (5 consecutive values)
            if (potentialHigh - potentialLow == 4)
            {
                return potentialHigh;
            }
        }
        
        // Check for wheel (A-2-3-4-5) - Ace can be low
        var wheelValues = new[] { 14, 5, 4, 3, 2 };
        if (wheelValues.All(distinctValues.Contains))
        {
            return 5; // 5-high straight (wheel)
        }
        
        // Fallback to highest card (shouldn't happen if hand is actually a straight)
        return distinctValues.First();
    }

    private static string FormatFlush(IReadOnlyCollection<Card> cards)
    {
        var flushSuit = cards
            .GroupBy(c => c.Suit)
            .Where(g => g.Count() >= 5)
            .Select(g => g.Key)
            .FirstOrDefault();

        var flushCards = cards.Where(c => c.Suit == flushSuit).ToList();
        var highCard = flushCards.OrderByDescending(c => c.Value).First();
        return $"Flush ({flushSuit}, {GetSymbolName(highCard.Symbol)}-high)";
    }

    private static string FormatFullHouse(IReadOnlyCollection<Card> cards)
    {
        var tripsValue = cards.ValueOfBiggestTrips();
        var pairValue = cards
            .GroupBy(c => c.Value)
            .Where(g => g.Count() >= 2 && g.Key != tripsValue)
            .OrderByDescending(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault();

        // Handle wild card hands where actual card pattern doesn't match
        if (tripsValue == 0 || pairValue == 0)
        {
            // Get the two highest distinct values for the description
            var distinctValues = cards
                .Select(c => c.Value)
                .Distinct()
                .OrderByDescending(v => v)
                .Take(2)
                .ToList();
            
            if (distinctValues.Count >= 2)
            {
                var highSymbol = distinctValues[0].ToSymbol();
                var lowSymbol = distinctValues[1].ToSymbol();
                return $"Full House ({GetPluralSymbolName(highSymbol)} full of {GetPluralSymbolName(lowSymbol)})";
            }
            return "Full House";
        }

        var tripsSymbol = tripsValue.ToSymbol();
        var pairSymbol = pairValue.ToSymbol();
        return $"Full House ({GetPluralSymbolName(tripsSymbol)} full of {GetPluralSymbolName(pairSymbol)})";
    }

    private static string FormatQuads(IReadOnlyCollection<Card> cards)
    {
        var quadsValue = cards
            .GroupBy(c => c.Value)
            .Where(g => g.Count() >= 4)
            .Select(g => g.Key)
            .FirstOrDefault();

        if (quadsValue == 0)
        {
            // Wild card hand - quads made with wild cards
            var highCard = cards.OrderByDescending(c => c.Value).First();
            return $"Four of a Kind ({GetPluralSymbolName(highCard.Symbol)})";
        }

        var symbol = quadsValue.ToSymbol();
        return $"Four of a Kind ({GetPluralSymbolName(symbol)})";
    }

    private static string FormatStraightFlush(IReadOnlyCollection<Card> cards)
    {
        var flushSuit = cards
            .GroupBy(c => c.Suit)
            .Where(g => g.Count() >= 5)
            .Select(g => g.Key)
            .FirstOrDefault();

        var flushCards = cards.Where(c => c.Suit == flushSuit).ToList();
        var straightHighValue = FindStraightHighValue(flushCards);
        
        // Check for Royal Flush (Ace-high straight flush)
        if (straightHighValue == 14)
        {
            return $"Royal Flush ({flushSuit})";
        }
        
        var symbol = straightHighValue.ToSymbol();
        return $"Straight Flush ({flushSuit}, {GetSymbolName(symbol)}-high)";
    }

    private static string FormatFiveOfAKind(IReadOnlyCollection<Card> cards)
    {
        var value = cards.First().Value;
        var symbol = value.ToSymbol();
        return $"Five of a Kind ({GetPluralSymbolName(symbol)})";
    }

    private static string GetSymbolName(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.Ace => "Ace",
            Symbol.King => "King",
            Symbol.Queen => "Queen",
            Symbol.Jack => "Jack",
            Symbol.Ten => "Ten",
            Symbol.Nine => "Nine",
            Symbol.Eight => "Eight",
            Symbol.Seven => "Seven",
            Symbol.Six => "Six",
            Symbol.Five => "Five",
            Symbol.Four => "Four",
            Symbol.Three => "Three",
            Symbol.Deuce => "Deuce",
            _ => symbol.ToString()
        };
    }

    private static string GetPluralSymbolName(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.Ace => "Aces",
            Symbol.King => "Kings",
            Symbol.Queen => "Queens",
            Symbol.Jack => "Jacks",
            Symbol.Ten => "Tens",
            Symbol.Nine => "Nines",
            Symbol.Eight => "Eights",
            Symbol.Seven => "Sevens",
            Symbol.Six => "Sixes",
            Symbol.Five => "Fives",
            Symbol.Four => "Fours",
            Symbol.Three => "Threes",
            Symbol.Deuce => "Deuces",
            _ => symbol.ToString()
        };
    }
}
