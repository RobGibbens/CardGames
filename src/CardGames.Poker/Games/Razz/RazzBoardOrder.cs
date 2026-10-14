using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.Razz;

/// <summary>
///     Action order for Razz. The highest door brings in, with ace as the lowest rank.
///     On later streets the best ace-to-five board acts first.
/// </summary>
public static class RazzBoardOrder
{
	public static int LowRank(int symbolValue) => symbolValue == 14 ? 1 : symbolValue;

	/// <summary>
	///     Higher suit brings in a tied door: spades, hearts, diamonds, clubs.
	/// </summary>
	public static int BringInSuitRank(Suit suit) => suit switch
	{
		Suit.Spades => 3,
		Suit.Hearts => 2,
		Suit.Diamonds => 1,
		Suit.Clubs => 0,
		_ => 0
	};

	/// <summary>
	///     True when <paramref name="door" /> is the worse Razz door and brings in ahead of <paramref name="other" />.
	/// </summary>
	public static bool BringsInOver(Card door, Card other)
	{
		var rank = LowRank(door.Value).CompareTo(LowRank(other.Value));
		if (rank != 0)
			return rank > 0;

		return BringInSuitRank(door.Suit) > BringInSuitRank(other.Suit);
	}

	public static long LowBoardStrength(IReadOnlyCollection<Card> cards) =>
		RazzHand.BoardStrength(cards);

	public static bool HasOpenPair(IEnumerable<int> symbolValues) =>
		symbolValues.GroupBy(LowRank).Any(group => group.Count() >= 2);
}
