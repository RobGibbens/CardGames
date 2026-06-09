using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.WildCards;

public class WildCardRules
{
	public WildCardRules(bool kingRequired = false)
	{
		KingRequired = kingRequired;
	}

	public bool KingRequired { get; }

	/// <summary>
	///     Determines wild cards treating Ace as high (value 14).
	/// </summary>
	public IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand)
	{
		return DetermineWildCardsCore(hand, false);
	}

	/// <summary>
	///     Determines wild cards treating Ace as low (value 1).
	///     When Ace is low, it becomes the lowest card and therefore wild.
	/// </summary>
	public IReadOnlyCollection<Card> DetermineWildCardsWithAceLow(IReadOnlyCollection<Card> hand)
	{
		return DetermineWildCardsCore(hand, true);
	}

	/// <summary>
	///     Returns all possible wild card sets for a hand, considering both Ace-high and Ace-low scenarios.
	///     If the hand contains no Aces, returns a single set. If it contains Aces, returns both possibilities.
	/// </summary>
	public IReadOnlyList<IReadOnlyCollection<Card>> GetAllPossibleWildCardSets(IReadOnlyCollection<Card> hand)
	{
		var aceHighWilds = DetermineWildCards(hand);

		var hasAce = hand.Any(c => c.Symbol == Symbol.Ace);
		if (!hasAce) return new[] { aceHighWilds };

		var aceLowWilds = DetermineWildCardsWithAceLow(hand);

		// If both sets are identical, return just one
		if (aceHighWilds.Count == aceLowWilds.Count &&
		    aceHighWilds.All(aceLowWilds.Contains))
			return [aceHighWilds];

		return [aceHighWilds, aceLowWilds];
	}

	private IReadOnlyCollection<Card> DetermineWildCardsCore(IReadOnlyCollection<Card> hand, bool aceAsLow)
	{
		var wildCards = new List<Card>();

		var kings = hand.Where(c => c.Symbol == Symbol.King).ToList();
		wildCards.AddRange(kings);

		var hasKing = kings.Any();
		if (!KingRequired || hasKing)
		{
			var nonKingCards = hand.Where(c => c.Symbol != Symbol.King).ToList();
			if (nonKingCards.Any())
			{
				var minValue = nonKingCards.Min(c => GetCardValue(c, aceAsLow));
				wildCards.AddRange(nonKingCards.Where(c => GetCardValue(c, aceAsLow) == minValue));
			}
		}

		return wildCards.Distinct().ToList();
	}

	private static int GetCardValue(Card card, bool aceAsLow)
	{
		if (aceAsLow && card.Symbol == Symbol.Ace) return 1;

		return card.Value;
	}
}