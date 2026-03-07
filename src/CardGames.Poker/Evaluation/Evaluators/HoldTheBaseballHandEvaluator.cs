using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Hold the Baseball (Texas Hold 'Em with 3s and 9s wild).
/// </summary>
[HandEvaluator("HOLDTHEBASEBALL")]
public sealed class HoldTheBaseballHandEvaluator : IHandEvaluator
{
	/// <inheritdoc />
	public bool SupportsPositionalCards => true;

	/// <inheritdoc />
	public bool HasWildCards => true;

	/// <inheritdoc />
	public HandBase CreateHand(IReadOnlyCollection<Card> cards)
	{
		var cardList = cards.ToList();
		var holeCards = cardList.Take(2).ToList();
		var communityCards = cardList.Skip(2).ToList();
		return new HoldTheBaseballHand(holeCards, communityCards);
	}

	/// <inheritdoc />
	public HandBase CreateHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> boardCards,
		IReadOnlyCollection<Card> downCards)
	{
		return new HoldTheBaseballHand(holeCards, boardCards);
	}

	/// <inheritdoc />
	public IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards)
	{
		var cardList = cards.ToList();
		var wildIndexes = new List<int>();

		for (var i = 0; i < cardList.Count; i++)
		{
			var symbol = cardList[i].Symbol;
			if (symbol == Symbol.Three || symbol == Symbol.Nine)
			{
				wildIndexes.Add(i);
			}
		}

		return wildIndexes;
	}

	/// <inheritdoc />
	public IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand)
	{
		return hand is HoldTheBaseballHand holdTheBaseballHand
			? holdTheBaseballHand.EvaluatedBestCards.ToList()
			: hand.Cards.ToList();
	}
}
