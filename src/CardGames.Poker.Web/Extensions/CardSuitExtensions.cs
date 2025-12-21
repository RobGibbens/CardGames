using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Extensions;

internal static class CardSuitExtensions
{
    internal static string ToCardSuitString(this CardSuit suit)
        => suit switch
        {
            CardSuit.Hearts => "Hearts",
            CardSuit.Diamonds => "Diamonds",
            CardSuit.Spades => "Spades",
            CardSuit.Clubs => "Clubs",
            _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unsupported card suit.")
        };

    internal static string ToCardSuitString(this CardSuit? suit)
        => suit is null ? "" : suit.Value.ToCardSuitString();
}
