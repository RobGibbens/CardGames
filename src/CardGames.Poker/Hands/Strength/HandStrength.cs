using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.Poker.Hands.Strength;

public static class HandStrength
{
	private static readonly long prefixMultiplier = 10000000000;

	public static HandType GetEffectiveType(IEnumerable<HandType> handTypes)
	{
		return handTypes
			.Select(type => new { type, Value = HandTypeStrength.Classic(type) })
			.OrderByDescending(pair => pair.Value)
			.First().type;
	}

	public static HandType GetEffectiveType(IEnumerable<HandType> handTypes, HandTypeStrengthRanking ranking)
	{
		return handTypes
			.Select(type => new { type, Value = HandTypeStrength.ByRanking(ranking, type) })
			.OrderByDescending(pair => pair.Value)
			.First().type;
	}

	public static long Calculate(IReadOnlyCollection<Card> cards, HandType handType, HandTypeStrengthRanking ranking)
	{
		return Calculate(cards, handType, HandTypeStrength.ByRanking(ranking, handType));
	}

	public static long Calculate(IReadOnlyCollection<Card> cards, HandType handType,
		Func<HandType, int> handTypeOrderMap)
	{
		return Calculate(cards, handType, handTypeOrderMap(handType));
	}

	private static long Calculate(IReadOnlyCollection<Card> cards, HandType handType, int handTypeStrengthMultiplier)
	{
		return prefixMultiplier * handTypeStrengthMultiplier + CalculateFromCards(cards, handType);
	}

	private static int CalculateFromCards(IReadOnlyCollection<Card> cards, HandType handType)
	{
		return OrderByPokerRank(cards, handType)
			.Select((value, index) => (int)(Math.Pow(10, 2 * (4 - index)) * value))
			.Sum();
	}

	/// <summary>
	///     Orders card values by poker significance: groups with more cards first (pairs, trips, etc.),
	///     then by value within each group size.
	///     For wheel straights (A-2-3-4-5), treats Ace as value 1 instead of 14.
	/// </summary>
	private static IEnumerable<int> OrderByPokerRank(IReadOnlyCollection<Card> cards, HandType handType)
	{
		// Check for wheel straight (A-2-3-4-5) or wheel straight flush
		if ((handType == HandType.Straight || handType == HandType.StraightFlush)
		    && HandTypeDetermination.IsWheelStraight(cards.DistinctValues()))
		{
			// Return wheel values with Ace as low (5-4-3-2-1)
			return new[] { 5, 4, 3, 2, 1 };
		}

		return cards
			.GroupBy(c => c.Value)
			.OrderByDescending(g => g.Count())
			.ThenByDescending(g => g.Key)
			.SelectMany(g => g)
			.Select(c => c.Value);
	}
}