using System.Collections.Generic;
using System.Linq;

namespace CardGames.Core.French.Cards.Extensions;

public static class CardsExtensions
{
	public static IReadOnlyCollection<Suit> Suits(this IEnumerable<Card> cards)
	{
		return
		[
			.. cards.SuitProjection()
		];
	}

	public static IReadOnlyCollection<Suit> DistinctSuits(this IEnumerable<Card> cards)
	{
		return
		[
			.. cards
				.Select(card => card.Suit)
				.Distinct()
		];
	}

	public static IReadOnlyCollection<int> Values(this IEnumerable<Card> cards)
	{
		return
		[
			.. cards
				.ValueProjection()
		];
	}

	public static IReadOnlyCollection<int> DistinctValues(this IEnumerable<Card> cards)
	{
		return cards
			.ValueProjection()
			.Distinct()
			.ToList();
	}

	public static IReadOnlyCollection<int> DescendingValues(this IEnumerable<Card> cards)
	{
		return cards
			.ByDescendingValue()
			.Values();
	}

	public static IReadOnlyCollection<int> DistinctDescendingValues(this IEnumerable<Card> cards)
	{
		return cards
			.ByDescendingValue()
			.ValueProjection()
			.Distinct()
			.ToList();
	}

	public static IReadOnlyCollection<Card> ByDescendingValue(this IEnumerable<Card> cards)
	{
		return cards
			.OrderByDescending(card => card.Value)
			.ToList();
	}

	public static int HighestValue(this IEnumerable<Card> cards)
	{
		return cards
			.ValueProjection()
			.Max();
	}

	public static int ValueOfBiggestPair(this IEnumerable<Card> cards)
	{
		return cards.HighestValueOfNFolds(2);
	}

	public static int ValueOfBiggestTrips(this IEnumerable<Card> cards)
	{
		return cards.HighestValueOfNFolds(3);
	}

	public static int ValueOfBiggestQuads(this IEnumerable<Card> cards)
	{
		return cards.HighestValueOfNFolds(4);
	}

	public static bool ContainsValues(this IEnumerable<Card> cards, IEnumerable<int> valuesToContain)
	{
		return valuesToContain.All(cards.Values().Contains);
	}

	public static bool ContainsValue(this IEnumerable<Card> cards, int value)
	{
		return cards
			.ValueProjection()
			.Contains(value);
	}

	private static int HighestValueOfNFolds(this IEnumerable<Card> cards, int nFold)
	{
		var nfolds = cards
			.GroupBy(card => card.Value)
			.Where(group => group.Count() >= nFold)
			.ToList();

		return nfolds.Any()
			? nfolds.Max(group => group.First().Value)
			: 0;
	}

	private static IEnumerable<int> ValueProjection(this IEnumerable<Card> cards)
	{
		return cards.Select(card => card.Value);
	}

	private static IEnumerable<Suit> SuitProjection(this IEnumerable<Card> cards)
	{
		return cards.Select(card => card.Suit);
	}
}