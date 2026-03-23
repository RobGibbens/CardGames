using System;
using System.Collections.Generic;

namespace CardGames.Poker.Web.Components.Shared;

public static class WildCardDisplayMatcher
{
	public static bool IsActiveWildCard(
		string? rank,
		string? suit,
		IReadOnlyList<TableCanvas.WildCardDisplay>? wildCards)
	{
		if (string.IsNullOrWhiteSpace(rank) || wildCards is null || wildCards.Count == 0)
		{
			return false;
		}

		var normalizedRank = NormalizeRank(rank);
		if (string.IsNullOrWhiteSpace(normalizedRank))
		{
			return false;
		}

		var normalizedSuit = NormalizeSuit(suit);

		foreach (var wildCard in wildCards)
		{
			if (string.IsNullOrWhiteSpace(wildCard.Rank))
			{
				continue;
			}

			if (!string.Equals(normalizedRank, NormalizeRank(wildCard.Rank), StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (string.IsNullOrWhiteSpace(wildCard.Suit))
			{
				return true;
			}

			if (string.Equals(normalizedSuit, NormalizeSuit(wildCard.Suit), StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private static string NormalizeRank(string? rank)
	{
		return rank?.Trim().ToUpperInvariant() switch
		{
			"A" or "ACE" => "A",
			"K" or "KING" => "K",
			"Q" or "QUEEN" => "Q",
			"J" or "JACK" => "J",
			"10" or "T" or "TEN" => "10",
			"9" or "NINE" => "9",
			"8" or "EIGHT" => "8",
			"7" or "SEVEN" => "7",
			"6" or "SIX" => "6",
			"5" or "FIVE" => "5",
			"4" or "FOUR" => "4",
			"3" or "THREE" => "3",
			"2" or "TWO" or "DEUCE" => "2",
			_ => string.Empty
		};
	}

	private static string NormalizeSuit(string? suit)
	{
		return suit?.Trim().ToUpperInvariant() switch
		{
			"C" or "CLUB" or "CLUBS" => "CLUBS",
			"D" or "DIAMOND" or "DIAMONDS" => "DIAMONDS",
			"H" or "HEART" or "HEARTS" => "HEARTS",
			"S" or "SPADE" or "SPADES" => "SPADES",
			_ => string.Empty
		};
	}
}