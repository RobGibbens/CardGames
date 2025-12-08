using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.CLI.Extensions;

/// <summary>
/// Extension methods for DealtCard from API contracts.
/// </summary>
internal static class DealtCardExtensions
{
    /// <summary>
    /// Converts a dealt card to its short string representation (e.g., "Ah", "Tc", "2d").
    /// </summary>
    internal static string ToShortString(this DealtCard card)
    {
        var symbolChar = card.Symbol switch
        {
            CardSymbol.Ace => 'A',
            CardSymbol.King => 'K',
            CardSymbol.Queen => 'Q',
            CardSymbol.Jack => 'J',
            CardSymbol.Ten => 'T',
            CardSymbol.Nine => '9',
            CardSymbol.Eight => '8',
            CardSymbol.Seven => '7',
            CardSymbol.Six => '6',
            CardSymbol.Five => '5',
            CardSymbol.Four => '4',
            CardSymbol.Three => '3',
            CardSymbol.Deuce => '2',
            _ => '?'
        };

        var suitChar = card.Suit switch
        {
            CardSuit.Hearts => 'h',
            CardSuit.Diamonds => 'd',
            CardSuit.Spades => 's',
            CardSuit.Clubs => 'c',
            _ => '?'
        };

        return $"{symbolChar}{suitChar}";
    }

    /// <summary>
    /// Converts a collection of dealt cards to a string representation, ordered by card value descending.
    /// </summary>
    internal static string ToStringRepresentation(this IEnumerable<DealtCard> cards)
        => string.Join(" ", cards.OrderByDescending(GetCardValue).Select(card => card.ToShortString()));

    /// <summary>
    /// Gets the numeric value of a card symbol for sorting purposes.
    /// </summary>
    private static int GetCardValue(DealtCard card) => card.Symbol switch
    {
        CardSymbol.Ace => 14,
        CardSymbol.King => 13,
        CardSymbol.Queen => 12,
        CardSymbol.Jack => 11,
        CardSymbol.Ten => 10,
        CardSymbol.Nine => 9,
        CardSymbol.Eight => 8,
        CardSymbol.Seven => 7,
        CardSymbol.Six => 6,
        CardSymbol.Five => 5,
        CardSymbol.Four => 4,
        CardSymbol.Three => 3,
        CardSymbol.Deuce => 2,
        _ => 0
    };
}
