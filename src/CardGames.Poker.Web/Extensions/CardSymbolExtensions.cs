using System.Globalization;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Extensions;

internal static class CardSymbolExtensions
{
    internal static string ToCardRankString(this CardSymbol symbol)
        => symbol switch
        {
            CardSymbol.Ace => "A",
            CardSymbol.King => "K",
            CardSymbol.Queen => "Q",
            CardSymbol.Jack => "J",
            CardSymbol.Ten => "10",
            _ => (GetNumericValue(symbol)).ToString(CultureInfo.InvariantCulture)
        };

    internal static string ToCardRankString(this CardSymbol? symbol)
        => symbol is null ? "?" : symbol.Value.ToCardRankString();

    private static int GetNumericValue(CardSymbol symbol)
        => symbol switch
        {
            CardSymbol.Deuce => 2,
            CardSymbol.Three => 3,
            CardSymbol.Four => 4,
            CardSymbol.Five => 5,
            CardSymbol.Six => 6,
            CardSymbol.Seven => 7,
            CardSymbol.Eight => 8,
            CardSymbol.Nine => 9,
            _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unsupported card symbol.")
        };
}
