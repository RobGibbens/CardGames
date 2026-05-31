using Entities = CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Shared mapping helpers that project stored card enums into the rank/suit/value
/// representations used by table-state DTOs.
/// </summary>
internal static class TableCardMapper
{
	internal static string MapSymbolToRank(Entities.CardSymbol symbol)
	{
		return symbol switch
		{
			Entities.CardSymbol.Ace => "A",
			Entities.CardSymbol.King => "K",
			Entities.CardSymbol.Queen => "Q",
			Entities.CardSymbol.Jack => "J",
			Entities.CardSymbol.Ten => "10",
			Entities.CardSymbol.Nine => "9",
			Entities.CardSymbol.Eight => "8",
			Entities.CardSymbol.Seven => "7",
			Entities.CardSymbol.Six => "6",
			Entities.CardSymbol.Five => "5",
			Entities.CardSymbol.Four => "4",
			Entities.CardSymbol.Three => "3",
			Entities.CardSymbol.Deuce => "2",
			_ => symbol.ToString()
		};
	}

	internal static string GetCardSuitString(Entities.CardSuit suit)
	{
		return suit switch
		{
			Entities.CardSuit.Hearts => "Hearts",
			Entities.CardSuit.Diamonds => "Diamonds",
			Entities.CardSuit.Spades => "Spades",
			Entities.CardSuit.Clubs => "Clubs",
			_ => suit.ToString()
		};
	}

	internal static int GetBobBarkerCardValue(Entities.CardSymbol symbol, bool aceHigh)
	{
		if (symbol == Entities.CardSymbol.Ace)
		{
			return aceHigh ? 14 : 1;
		}

		return (int)symbol;
	}
}
