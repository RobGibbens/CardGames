using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Evaluation.Evaluators;

/// <summary>
/// Hand evaluator for Baseball poker (7-card stud with 3s and 9s wild).
/// </summary>
[HandEvaluator("BASEBALL")]
public sealed class BaseballHandEvaluator : IHandEvaluator
{
	private readonly BaseballWildCardRules _wildCardRules = new();

	/// <inheritdoc />
	public bool SupportsPositionalCards => true;

	/// <inheritdoc />
	public bool HasWildCards => true;

	/// <inheritdoc />
	public HandBase CreateHand(IReadOnlyCollection<Card> cards)
	{
		var cardList = cards.ToList();
		var holeCards = cardList.Take(2).ToList();
		var openCards = cardList.Skip(2).ToList();

		return new BaseballHand(holeCards, openCards, []);
	}

	/// <inheritdoc />
	public HandBase CreateHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> boardCards,
		IReadOnlyCollection<Card> downCards)
	{
		var combinedHoleCards = holeCards.Concat(downCards).ToList();
		return new BaseballHand(combinedHoleCards, boardCards.ToList(), []);
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
		return hand is BaseballHand baseballHand
			? baseballHand.EvaluatedBestCards.ToList()
			: hand.Cards.ToList();
	}
}
