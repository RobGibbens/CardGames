using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Core.French.Decks;

public sealed class FullFrenchDeck : FrenchDeck
{
	protected override IReadOnlyCollection<Card> Cards { get; } =
	[
		.. Enumerable
			.Range(2, 13)
			.SelectMany(CardsOfValue)
	];

	private static IEnumerable<Card> CardsOfValue(int value)
	{
		return Enumerable
			.Range(0, 4)
			.Select(suit => new Card((Suit)suit, value));
	}
}