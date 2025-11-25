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
        var symbol = tripsValue.ToSymbol();
        return $"Three of a Kind ({GetPluralSymbolName(symbol)})";
    }

    private static string FormatStraight(IReadOnlyCollection<Card> cards)
    {
        var highCard = cards.OrderByDescending(c => c.Value).First();
        return $"Straight ({GetSymbolName(highCard.Symbol)}-high)";
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
            .First();

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
        var highCard = flushCards.OrderByDescending(c => c.Value).First();
        
        // Check for Royal Flush
        if (highCard.Value == 14) // Ace
        {
            var values = flushCards.Select(c => c.Value).OrderByDescending(v => v).Take(5).ToList();
            if (values.SequenceEqual(new[] { 14, 13, 12, 11, 10 }))
            {
                return $"Royal Flush ({flushSuit})";
            }
        }
        
        return $"Straight Flush ({flushSuit}, {GetSymbolName(highCard.Symbol)}-high)";
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
