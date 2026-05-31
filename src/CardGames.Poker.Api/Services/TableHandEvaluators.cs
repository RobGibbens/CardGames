using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Pure, variant-aware hand-evaluation and best-hand projection helpers used while building
/// table state. These contain no data-access or instance state and operate solely on the
/// supplied cards, so they can be reasoned about and tested in isolation.
/// </summary>
internal static class TableHandEvaluators
{
	private static readonly Dictionary<string, Func<List<Card>, HandBase>> DrawHandFactories =
		new(TableVariantClassifier.GameCodeComparer)
		{
			[PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode] = cards => new TwosJacksManWithTheAxeDrawHand(cards),
			[PokerGameMetadataRegistry.KingsAndLowsCode] = cards => new KingsAndLowsDrawHand(cards)
		};

	internal static readonly Dictionary<string, Func<List<GameCard>, List<Card>, string>> StudVariantEvaluators =
		new(TableVariantClassifier.GameCodeComparer)
		{
			[PokerGameMetadataRegistry.BaseballCode] = EvaluateBaseballHandDescription,
			[PokerGameMetadataRegistry.RazzCode] = EvaluateRazzHandDescription
		};

	internal static int GetStreetPhaseOrder(string? phase) => phase switch
	{
		"ThirdStreet" => 1,
		"FourthStreet" => 2,
		"FifthStreet" => 3,
		"SixthStreet" => 4,
		"SeventhStreet" => 5,
		_ => 99 // Unknown phases sort last, not first
	};

	internal static HandBase BuildDrawHandForGame(string? gameTypeCode, List<Card> playerCards)
	{
		if (!string.IsNullOrWhiteSpace(gameTypeCode) && DrawHandFactories.TryGetValue(gameTypeCode, out var factory))
		{
			return factory(playerCards);
		}

		return new DrawHand(playerCards);
	}

	internal static string EvaluateBaseballHandDescription(List<GameCard> holeCardEntities, List<Card> openCards)
	{
		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();
		var baseballHand = new BaseballHand(allHoleCards, openCards, []);
		return HandDescriptionFormatter.GetHandDescription(baseballHand);
	}

	internal static string? EvaluateSevenCardStudHandDescription(List<GameCard> holeCardEntities, List<Card> initialHoleCards, List<Card> openCards)
	{
		if (holeCardEntities.Count >= 3 && openCards.Count <= 4)
		{
			var downCard = new Card((Suit)holeCardEntities[2].Suit, (Symbol)holeCardEntities[2].Symbol);
			var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
			return HandDescriptionFormatter.GetHandDescription(studHand);
		}

		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();
		var partialStudHand = new StudHand(initialHoleCards, openCards, allHoleCards.Skip(2).ToList());
		return HandDescriptionFormatter.GetHandDescription(partialStudHand);
	}

	internal static string EvaluateRazzHandDescription(List<GameCard> holeCardEntities, List<Card> openCards)
	{
		var allHoleCards = holeCardEntities
			.Select(c => new Card((Suit)c.Suit, (Symbol)c.Symbol))
			.ToList();

		var initialHoleCards = allHoleCards.Take(2).ToList();
		var downCards = allHoleCards.Skip(2).ToList();
		var razzHand = new RazzHand(initialHoleCards, openCards, downCards);
		return RazzHand.GetLowHandDescription(razzHand.GetBestLowHand());
	}

	/// <summary>
	/// Orders cards in the correct deal sequence, handling Seven Card Stud's multi-street dealing.
	/// For stud games, uses a composite key based on phase and location; for other games, falls back to DealOrder.
	/// </summary>
	/// <param name="cards">The collection of cards to order.</param>
	/// <param name="isSevenCardStud">Whether this is a Seven Card Stud game.</param>
	/// <returns>Cards ordered in the correct deal sequence.</returns>
	internal static IEnumerable<GameCard> OrderCardsForDisplay(IEnumerable<GameCard> cards, bool isSevenCardStud)
	{
		if (isSevenCardStud)
		{
			return StudOrderHelper.OrderPlayerCards(
				cards,
				card => card.DealtAtPhase,
				card => card.Location == CardLocation.Hole,
				card => card.DealOrder);
		}

		// For other games: Order by DealOrder which should be sequential per player
		return cards.OrderBy(c => c.DealOrder);
	}

	internal static List<int> GetCardIndexes(List<Card> allCards, IEnumerable<Card> targetCards)
	{
		var indexes = new List<int>();
		var usedIndexes = new HashSet<int>();

		foreach (var target in targetCards)
		{
			for (var i = 0; i < allCards.Count; i++)
			{
				if (usedIndexes.Contains(i)) continue;

				if (allCards[i].Equals(target))
				{
					indexes.Add(i);
					usedIndexes.Add(i);
					break;
				}
			}
		}
		return indexes;
	}

	/// <summary>
	/// Finds the best 5-card hand from a set of cards (e.g., 7 cards in Hold'Em)
	/// by evaluating all C(n,5) combinations for the strongest hand.
	/// </summary>
	internal static List<Card> FindBestFiveCardHand(List<Card> allCards)
	{
		if (allCards.Count <= 5)
		{
			return allCards;
		}

		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var combo in allCards.SubsetsOfSize(5))
		{
			var comboList = combo.ToList();
			var handType = HandTypeDetermination.DetermineHandType(comboList);
			var strength = HandStrength.Calculate(comboList, handType, ranking);
			if (strength > bestStrength)
			{
				bestStrength = strength;
				bestCombo = comboList;
			}
		}

		return bestCombo ?? allCards.Take(5).ToList();
	}

	/// <summary>
	/// Finds the best 5-card Omaha hand using exactly 2 hole cards + 3 community cards.
	/// Evaluates all C(4,2) × C(5,3) = 60 valid combinations.
	/// </summary>
	internal static List<Card> FindBestOmahaHand(List<Card> holeCards, List<Card> communityCards)
	{
		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var holePair in holeCards.SubsetsOfSize(2))
		{
			foreach (var communityTriple in communityCards.SubsetsOfSize(3))
			{
				var combo = holePair.Concat(communityTriple).ToList();
				var handType = HandTypeDetermination.DetermineHandType(combo);
				var strength = HandStrength.Calculate(combo, handType, ranking);
				if (strength > bestStrength)
				{
					bestStrength = strength;
					bestCombo = combo;
				}
			}
		}

		return bestCombo ?? holeCards.Take(2).Concat(communityCards.Take(3)).ToList();
	}

	/// <summary>
	/// Finds the best 5-card Nebraska hand using exactly 3 hole cards + 2 community cards.
	/// Evaluates all C(5,3) × C(5,2) = 100 valid combinations.
	/// </summary>
	internal static List<Card> FindBestNebraskaHand(List<Card> holeCards, List<Card> communityCards)
	{
		var ranking = HandTypeStrengthRanking.Classic;
		List<Card>? bestCombo = null;
		long bestStrength = long.MinValue;

		foreach (var holeTriple in holeCards.SubsetsOfSize(3))
		{
			foreach (var communityPair in communityCards.SubsetsOfSize(2))
			{
				var combo = holeTriple.Concat(communityPair).ToList();
				var handType = HandTypeDetermination.DetermineHandType(combo);
				var strength = HandStrength.Calculate(combo, handType, ranking);
				if (strength > bestStrength)
				{
					bestStrength = strength;
					bestCombo = combo;
				}
			}
		}

		return bestCombo ?? holeCards.Take(3).Concat(communityCards.Take(2)).ToList();
	}
}
