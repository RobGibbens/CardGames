using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.DealHands;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CoreSuit = CardGames.Core.French.Cards.Suit;
using CoreSymbol = CardGames.Core.French.Cards.Symbol;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Service for calculating poker hand odds based on current cards.
/// </summary>
public static class HandOddsCalculationService
{
	/// <summary>
	/// Calculates the odds of achieving each hand type for a Five Card Draw hand.
	/// </summary>
	/// <param name="heroCards">The player's current hand cards.</param>
	/// <param name="deadCards">Cards that are known to be out of play (e.g., folded players' cards).</param>
	/// <returns>Hand odds response with probabilities for each hand type.</returns>
	public static HandOddsResponse CalculateDrawOdds(
		IReadOnlyList<DealtCard> heroCards,
		IReadOnlyList<DealtCard>? deadCards = null)
	{
		var coreHeroCards = heroCards
			.Select(ConvertDealtCardToCard)
			.ToList();

		var coreDeadCards = deadCards?
			.Select(ConvertDealtCardToCard)
			.ToList() ?? [];

		var result = OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds(coreHeroCards, coreDeadCards);

		return ConvertToResponse(result);
	}

	/// <summary>
	/// Calculates the odds of achieving each hand type for a Five Card Draw hand.
	/// </summary>
	/// <param name="heroCards">The player's current hand cards as GameCard entities.</param>
	/// <param name="deadCards">Cards that are known to be out of play.</param>
	/// <returns>Hand odds response with probabilities for each hand type.</returns>
	public static HandOddsResponse CalculateDrawOdds(
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

		var result = OddsCalculator.CalculateTwosJacksManWithTheAxeDrawOdds(coreHeroCards, coreDeadCards);

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
		// Order hand types from best to worst for display
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
