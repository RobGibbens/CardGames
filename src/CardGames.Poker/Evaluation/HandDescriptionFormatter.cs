using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.Poker.Evaluation;

public static class HandDescriptionFormatter
{
	public static string GetHandDescription(HandBase hand)
	{
		if (hand is null)
		{
			throw new ArgumentNullException(nameof(hand));
		}

		var cards = hand.Cards.ToList();

		return hand.Type switch
		{
			HandType.HighCard => FormatHighCard(cards),
			HandType.OnePair => FormatOnePair(cards),
			HandType.TwoPair => FormatTwoPair(cards),
			HandType.Trips => FormatTrips(cards),
			HandType.Straight => FormatStraight(cards),
			HandType.Flush => FormatFlush(cards),
			HandType.FullHouse => FormatFullHouse(cards),
			HandType.Quads => FormatQuads(cards),
			HandType.StraightFlush => FormatStraightFlush(cards),
			HandType.FiveOfAKind => FormatFiveOfAKind(cards),
			_ => "Incomplete Hand"
		};
	}

	private static string FormatHighCard(IReadOnlyCollection<Card> cards)
	{
		var highCard = cards.OrderByDescending(c => c.Value).First();
		return $"{GetSymbolName(highCard.Symbol)} high";
	}

	private static string FormatOnePair(IReadOnlyCollection<Card> cards)
	{
		var pairValue = cards.ValueOfBiggestPair();
		var symbol = pairValue.ToSymbol();
		return $"Pair of {GetPluralSymbolName(symbol)}";
	}

	private static string FormatTwoPair(IReadOnlyCollection<Card> cards)
	{
		var pairs = cards
			.GroupBy(c => c.Value)
			.Where(g => g.Count() >= 2)
			.OrderByDescending(g => g.Key)
			.Take(2)
			.Select(g => g.Key.ToSymbol())
			.ToList();

		return pairs.Count >= 2
			? $"Two pair, {GetPluralSymbolName(pairs[0])} and {GetPluralSymbolName(pairs[1])}"
			: "Two pair";
	}

	private static string FormatTrips(IReadOnlyCollection<Card> cards)
	{
		var tripsValue = cards.ValueOfBiggestTrips();
		var symbol = tripsValue.ToSymbol();
		return $"Three of a kind, {GetPluralSymbolName(symbol)}";
	}

	private static string FormatStraight(IReadOnlyCollection<Card> cards)
	{
		var straightHighValue = FindStraightHighValue(cards);
		var symbol = straightHighValue.ToSymbol();
		return $"Straight to the {GetSymbolName(symbol)}";
	}

	private static string FormatFlush(IReadOnlyCollection<Card> cards)
	{
		var flushSuit = cards
			.GroupBy(c => c.Suit)
			.Where(g => g.Count() >= 5)
			.Select(g => g.Key)
			.FirstOrDefault();

		var flushCards = cards.Where(c => c.Suit == flushSuit).ToList();
		var highCard = flushCards.OrderByDescending(c => c.Value).First();
		return $"{GetSymbolName(highCard.Symbol)} high flush";
	}

	private static string FormatFullHouse(IReadOnlyCollection<Card> cards)
	{
		var tripsValue = cards.ValueOfBiggestTrips();
		var pairValue = cards
			.GroupBy(c => c.Value)
			.Where(g => g.Count() >= 2 && g.Key != tripsValue)
			.OrderByDescending(g => g.Key)
			.Select(g => g.Key)
			.FirstOrDefault();

		var tripsSymbol = tripsValue.ToSymbol();
		var pairSymbol = pairValue.ToSymbol();
		return $"Full house, {GetPluralSymbolName(tripsSymbol)} full of {GetPluralSymbolName(pairSymbol)}";
	}

	private static string FormatQuads(IReadOnlyCollection<Card> cards)
	{
		var quadsValue = cards
			.GroupBy(c => c.Value)
			.Where(g => g.Count() >= 4)
			.Select(g => g.Key)
			.First();

		var symbol = quadsValue.ToSymbol();
		return $"Four of a kind, {GetPluralSymbolName(symbol)}";
	}

	private static string FormatStraightFlush(IReadOnlyCollection<Card> cards)
	{
		var flushSuit = cards
			.GroupBy(c => c.Suit)
			.Where(g => g.Count() >= 5)
			.Select(g => g.Key)
			.FirstOrDefault();

		var flushCards = cards.Where(c => c.Suit == flushSuit).ToList();
		var straightHighValue = FindStraightHighValue(flushCards);

		return straightHighValue == 14
			? "Royal flush"
			: $"Straight flush to the {GetSymbolName(straightHighValue.ToSymbol())}";
	}

	private static string FormatFiveOfAKind(IReadOnlyCollection<Card> cards)
	{
		var value = cards.First().Value;
		var symbol = value.ToSymbol();
		return $"Five of a kind, {GetPluralSymbolName(symbol)}";
	}

	private static int FindStraightHighValue(IReadOnlyCollection<Card> cards)
	{
		var distinctValues = cards.DistinctDescendingValues().ToList();

		for (int i = 0; i <= distinctValues.Count - 5; i++)
		{
			var potentialHigh = distinctValues[i];
			var potentialLow = distinctValues[i + 4];
			if (potentialHigh - potentialLow == 4)
			{
				return potentialHigh;
			}
		}

		// Wheel (A-2-3-4-5)
		var wheelValues = new[] { 14, 5, 4, 3, 2 };
		if (wheelValues.All(distinctValues.Contains))
		{
			return 5;
		}

		return distinctValues.First();
	}

	private static string GetSymbolName(Symbol symbol)
	{
		return symbol switch
		{
			Symbol.Ace => "Ace",
			Symbol.King => "King",
			Symbol.Queen => "Queen",
			Symbol.Jack => "Jack",
			Symbol.Ten => "Ten",
			Symbol.Nine => "Nine",
			Symbol.Eight => "Eight",
			Symbol.Seven => "Seven",
			Symbol.Six => "Six",
			Symbol.Five => "Five",
			Symbol.Four => "Four",
			Symbol.Three => "Three",
			Symbol.Deuce => "Deuce",
			_ => symbol.ToString()
		};
	}

	private static string GetPluralSymbolName(Symbol symbol)
	{
		return symbol switch
		{
			Symbol.Ace => "Aces",
			Symbol.King => "Kings",
			Symbol.Queen => "Queens",
			Symbol.Jack => "Jacks",
			Symbol.Ten => "Tens",
			Symbol.Nine => "Nines",
			Symbol.Eight => "Eights",
			Symbol.Seven => "Sevens",
			Symbol.Six => "Sixes",
			Symbol.Five => "Fives",
			Symbol.Four => "Fours",
			Symbol.Three => "Threes",
			Symbol.Deuce => "Deuces",
			_ => symbol.ToString()
		};
	}
}
