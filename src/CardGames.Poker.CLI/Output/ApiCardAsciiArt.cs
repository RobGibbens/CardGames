using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Provides ASCII art representations for playing cards.
/// </summary>
internal static class ApiCardAsciiArt
{
    private const int CardWidth = 9;
    private const int CardHeight = 7;
    private const string WildCardColor = "green";

    /// <summary>
    /// Gets the ASCII art lines for a card showing its face.
    /// </summary>
    internal static IReadOnlyList<string> GetCardFace(DealtCard card)
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
    internal static string GetSuitColor(CardSuit? suit) => suit switch
    {
	    CardSuit.Hearts or CardSuit.Diamonds => "red",
        CardSuit.Spades or CardSuit.Clubs => "white",
        _ => "white"
    };

    /// <summary>
    /// Gets the Spectre Console color markup for a card, considering wild cards.
    /// Wild cards are displayed in green regardless of suit.
    /// </summary>
    internal static string GetCardColor(DealtCard card, IEnumerable<DealtCard> wildCards = null)
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
    internal static string GetSuitChar(CardSuit? suit) => suit switch
    {
	    CardSuit.Hearts => "♥",
	    CardSuit.Diamonds => "♦",
	    CardSuit.Spades => "♠",
	    CardSuit.Clubs => "♣",
        _ => "?"
    };

    /// <summary>
    /// Gets the display character(s) for a card symbol.
    /// </summary>
    internal static string GetSymbolChar(CardSymbol? symbol) => symbol switch
    {
	    CardSymbol.Ace => "A",
        CardSymbol.King => "K",
        CardSymbol.Queen => "Q",
        CardSymbol.Jack => "J",
        CardSymbol.Ten => "10",
        CardSymbol.Nine => "9",
        CardSymbol.Eight => "8",
        CardSymbol.Seven => "7",
        CardSymbol.Six => "6",
        CardSymbol.Five => "5",
        CardSymbol.Four => "4",
        CardSymbol.Three => "3",
        CardSymbol.Deuce => "2",
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
