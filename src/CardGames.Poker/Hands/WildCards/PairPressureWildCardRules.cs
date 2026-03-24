using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.WildCards;

/// <summary>
/// Pair Pressure wild card rules.
/// - Whenever a face-up rank appears for the second time, that rank becomes wild.
/// - Only the two most recent paired-up ranks remain active.
/// - When a third distinct paired rank appears, the oldest active wild rank is evicted.
/// </summary>
public class PairPressureWildCardRules
{
	public IReadOnlyCollection<Card> DetermineWildCards(
		IReadOnlyCollection<Card> hand,
		IReadOnlyCollection<Card> faceUpCardsInOrder)
	{
		var wildRanks = DetermineWildRanks(faceUpCardsInOrder);
		return hand.Where(card => wildRanks.Contains(card.Value)).ToList();
	}

	public IReadOnlyCollection<int> DetermineWildRanks(IReadOnlyCollection<Card> faceUpCardsInOrder)
	{
		var rankCounts = new Dictionary<int, int>();
		var pairedRanksSeen = new HashSet<int>();
		var activeWildRanks = new Queue<int>();

		foreach (var card in faceUpCardsInOrder)
		{
			rankCounts.TryGetValue(card.Value, out var currentCount);
			currentCount++;
			rankCounts[card.Value] = currentCount;

			if (currentCount != 2 || !pairedRanksSeen.Add(card.Value))
			{
				continue;
			}

			activeWildRanks.Enqueue(card.Value);
			if (activeWildRanks.Count > 2)
			{
				activeWildRanks.Dequeue();
			}
		}

		return activeWildRanks.ToList();
	}
}