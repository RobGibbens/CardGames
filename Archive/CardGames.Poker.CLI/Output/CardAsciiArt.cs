using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Provides ASCII art representations for playing cards.
/// </summary>
internal static class CardAsciiArt
{
    private const int CardWidth = 9;
    private const int CardHeight = 7;
    private const string WildCardColor = "green";

    /// <summary>
    /// Gets the ASCII art lines for a card showing its face.
    /// </summary>
    internal static IReadOnlyList<string> GetCardFace(Card card)
    {
        var symbol = GetSymbolChar(card.Symbol);
        var suit = GetSuitChar(card.Suit);
        var paddedSymbol = symbol.Length == 1 ? symbol + " " : symbol;
        var paddedSymbolRight = symbol.Length == 1 ? " " + symbol : symbol;

        return new[]
        {
            "┌───────┐",
            $"│{paddedSymbol}     │",
            "│       │",
            $"│   {suit}   │",
            "│       │",
            $"│     {paddedSymbolRight}│",
            "└───────┘"
        };
    }

    /// <summary>
    /// Gets the ASCII art lines for the back of a card.
    /// </summary>
    internal static IReadOnlyList<string> GetCardBack()
    {
        return new[]
        {
            "┌───────┐",
            "│░░░░░░░│",
            "│░░░░░░░│",
            "│░░░░░░░│",
            "│░░░░░░░│",
            "│░░░░░░░│",
            "└───────┘"
        };
    }

    /// <summary>
    /// Gets the Spectre Console color markup for a suit.
    /// </summary>
    internal static string GetSuitColor(Suit suit) => suit switch
    {
        Suit.Hearts or Suit.Diamonds => "red",
        Suit.Spades or Suit.Clubs => "white",
        _ => "white"
    };

    /// <summary>
    /// Gets the Spectre Console color markup for a card, considering wild cards.
    /// Wild cards are displayed in green regardless of suit.
    /// </summary>
    internal static string GetCardColor(Card card, IEnumerable<Card> wildCards = null)
    {
        if (wildCards != null && wildCards.Contains(card))
        {
            return WildCardColor;
        }
        return GetSuitColor(card.Suit);
    }

    /// <summary>
    /// Gets the Unicode character for a suit.
    /// </summary>
    internal static string GetSuitChar(Suit suit) => suit switch
    {
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Spades => "♠",
        Suit.Clubs => "♣",
        _ => "?"
    };

    /// <summary>
    /// Gets the display character(s) for a card symbol.
    /// </summary>
    internal static string GetSymbolChar(Symbol symbol) => symbol switch
    {
        Symbol.Ace => "A",
        Symbol.King => "K",
        Symbol.Queen => "Q",
        Symbol.Jack => "J",
        Symbol.Ten => "10",
        Symbol.Nine => "9",
        Symbol.Eight => "8",
        Symbol.Seven => "7",
        Symbol.Six => "6",
        Symbol.Five => "5",
        Symbol.Four => "4",
        Symbol.Three => "3",
        Symbol.Deuce => "2",
        _ => "?"
    };

    /// <summary>
    /// Gets the width of a single card in characters.
    /// </summary>
    internal static int Width => CardWidth;

    /// <summary>
    /// Gets the height of a single card in lines.
    /// </summary>
    internal static int Height => CardHeight;
}
