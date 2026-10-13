using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.CommunityCardHands;

/// <summary>
///     Nebraska hand evaluator.
///     Must use exactly 3 hole cards and 2 community cards.
/// </summary>
public sealed class NebraskaHand : CommunityCardsHand
{
	public NebraskaHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
		: base(3, 3, holeCards, communityCards, HandTypeStrengthRanking.Classic)
	{
	}

	/// <summary>
	///     A legal hand needs three hole cards and two community cards. Earlier streets have no
	///     such combination, so report an incomplete hand instead of enumerating an empty set.
	/// </summary>
	protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
	{
		if (HoleCards.Count < 3 || CommunityCards.Count < 2)
			return [new List<Card>()];

		return base.PossibleHands();
	}

	/// <summary>
	///     Gets the evaluated best 5-card hand honoring Nebraska rules (exactly 3 hole + 2 community).
	/// </summary>
	public IReadOnlyCollection<Card> EvaluatedBestCards => field ??= FindBestFiveCardHand();

	private IReadOnlyCollection<Card> FindBestFiveCardHand()
	{
		return PossibleHands()
			.Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) })
			.Where(pair => pair.type == Type)
			.OrderByDescending(pair => HandStrength.Calculate(pair.hand.ToList(), pair.type, Ranking))
			.First()
			.hand
			.ToList();
	}
}