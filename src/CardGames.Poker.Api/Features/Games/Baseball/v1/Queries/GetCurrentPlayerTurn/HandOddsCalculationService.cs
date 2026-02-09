using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CoreSuit = CardGames.Core.French.Cards.Suit;
using CoreSymbol = CardGames.Core.French.Cards.Symbol;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public static class HandOddsCalculationService
{
	public static HandOddsResponse CalculateBaseballOdds(
		IReadOnlyList<DealtCard> heroCards,
		IReadOnlyList<DealtCard>? deadCards = null)
	{
		var coreHeroCards = heroCards
			.Select(ConvertDealtCardToCard)
			.ToList();

		var coreDeadCards = deadCards?
			.Select(ConvertDealtCardToCard)
			.ToList() ?? [];

		var holeCards = coreHeroCards.Take(2).ToList();
		var boardCards = coreHeroCards.Skip(2).ToList();
		var totalCards = Math.Max(7, coreHeroCards.Count);

		var result = OddsCalculator.CalculateBaseballOdds(holeCards, boardCards, totalCards, coreDeadCards);
		return ConvertToResponse(result);
	}

	public static HandOddsResponse CalculateBaseballOdds(
		IEnumerable<GameCard> heroCards,
		IEnumerable<GameCard>? deadCards = null)
	{
		var coreHeroCards = heroCards
			.Where(c => !c.IsDiscarded)
			.Select(ConvertGameCardToCard)
			.ToList();

		var coreDeadCards = deadCards?
			.Select(ConvertGameCardToCard)
			.ToList() ?? [];

		var holeCards = coreHeroCards.Take(2).ToList();
		var boardCards = coreHeroCards.Skip(2).ToList();
		var totalCards = Math.Max(7, coreHeroCards.Count);

		var result = OddsCalculator.CalculateBaseballOdds(holeCards, boardCards, totalCards, coreDeadCards);
		return ConvertToResponse(result);
	}

	private static Card ConvertDealtCardToCard(DealtCard dealtCard)
	{
		var suit = (CoreSuit)(int)dealtCard.Suit;
		var symbol = (CoreSymbol)(int)dealtCard.Symbol;

		return new Card(suit, symbol);
	}

	private static Card ConvertGameCardToCard(GameCard gameCard)
	{
		var suit = (CoreSuit)(int)gameCard.Suit;
		var symbol = (CoreSymbol)(int)gameCard.Symbol;

		return new Card(suit, symbol);
	}

	private static HandOddsResponse ConvertToResponse(OddsCalculator.OddsResult result)
	{
		var orderedHandTypes = new[]
		{
			HandType.FiveOfAKind,
			HandType.StraightFlush,
			HandType.Quads,
			HandType.FullHouse,
			HandType.Flush,
			HandType.Straight,
			HandType.Trips,
			HandType.TwoPair,
			HandType.OnePair,
			HandType.HighCard
		};

		var probabilities = orderedHandTypes
			.Where(ht => result.HandTypeProbabilities.ContainsKey(ht))
			.Select(ht => new HandTypeOdds(
				HandType: ht.ToString(),
				DisplayName: GetDisplayName(ht),
				Probability: result.HandTypeProbabilities[ht]
			))
			.ToList();

		return new HandOddsResponse(probabilities);
	}

	private static string GetDisplayName(HandType handType) => handType switch
	{
		HandType.HighCard => "High Card",
		HandType.OnePair => "One Pair",
		HandType.TwoPair => "Two Pair",
		HandType.Trips => "Three of a Kind",
		HandType.Straight => "Straight",
		HandType.Flush => "Flush",
		HandType.FullHouse => "Full House",
		HandType.Quads => "Four of a Kind",
		HandType.StraightFlush => "Straight Flush",
		HandType.FiveOfAKind => "Five of a Kind",
		_ => handType.ToString()
	};
}
