using System.Collections.Generic;
using System.Linq;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.CommunityCardHands;

public class CommunityCardsHand : HandBase
{
	protected readonly int GreatestNumberOfHoleCardsToBeUsed;
	protected readonly int LeastNumberOfHoleCardsToBeUsed;

	public CommunityCardsHand(
		int leastNumberOfHoleCardsToBeUsed,
		int greatestNumberOfHoleCardsToBeUsed,
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> communityCards,
		HandTypeStrengthRanking ranking)
		: base([
			.. holeCards,
			.. communityCards
		])
	{
		LeastNumberOfHoleCardsToBeUsed = leastNumberOfHoleCardsToBeUsed;
		GreatestNumberOfHoleCardsToBeUsed = greatestNumberOfHoleCardsToBeUsed;
		HoleCards = holeCards;
		CommunityCards = communityCards;
		Ranking = ranking;
	}

	public IReadOnlyCollection<Card> HoleCards { get; }
	public IReadOnlyCollection<Card> CommunityCards { get; }
	protected override HandTypeStrengthRanking Ranking { get; }

	protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
	{
		if (Cards.Count < 5) return new[] { Cards };

		var nrOfCombos = GreatestNumberOfHoleCardsToBeUsed - LeastNumberOfHoleCardsToBeUsed + 1;
		return Enumerable
			.Range(LeastNumberOfHoleCardsToBeUsed, nrOfCombos)
			.SelectMany(PossibleHandsWithFixedNumberOfHoleCards);
	}

	private IEnumerable<IReadOnlyCollection<Card>> PossibleHandsWithFixedNumberOfHoleCards(int numberOfHoleCards)
	{
		var communityCombos = CommunityCards.SubsetsOfSize(5 - numberOfHoleCards);

		return numberOfHoleCards == 0
			? communityCombos.Select(cards => cards.ToList())
			: HoleCards.SubsetsOfSize(numberOfHoleCards)
				.CartesianProduct(communityCombos)
				.Select(sequences => sequences
					.SelectMany(cards => cards)
					.ToList());
	}
}