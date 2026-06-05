using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Simulations.Draw;

/// <summary>
///     Represents a player in a Five Card Draw simulation.
/// </summary>
public class FiveCardDrawPlayer
{
	public FiveCardDrawPlayer(string name)
	{
		Name = name;
	}

	public IReadOnlyCollection<Card> GivenCards { get; private set; } = [];
	public IReadOnlyCollection<Card> DealtCards { get; set; } = [];

	public IEnumerable<Card> Cards => GivenCards.Concat(DealtCards);

	public string Name { get; }

	public FiveCardDrawPlayer WithCards(IReadOnlyCollection<Card> cards)
	{
		if (cards.Count > 5)
		{
			throw new ArgumentException($"{Name} has too many cards to play Five Card Draw.");
		}

		GivenCards = cards;
		return this;
	}
}