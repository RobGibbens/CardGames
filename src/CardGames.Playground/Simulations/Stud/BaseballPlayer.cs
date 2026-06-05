using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Playground.Simulations.Stud;

/// <summary>
///     Baseball player that can have extra cards from receiving 4s face up.
///     In baseball, when a player is dealt a 4 face up, they receive an extra face up card.
/// </summary>
public class BaseballPlayer
{
	public BaseballPlayer(string name)
	{
		Name = name;
	}

	public IReadOnlyCollection<Card> GivenHoleCards { get; private set; } = [];
	public IReadOnlyCollection<Card> DealtHoleCards { get; set; } = [];
	public IReadOnlyCollection<Card> GivenBoardCards { get; private set; } = [];
	public IReadOnlyCollection<Card> DealtBoardCards { get; set; } = [];
	public IReadOnlyCollection<Card> ExtraBoardCards { get; set; } = [];

	public IEnumerable<Card> HoleCards => GivenHoleCards.Concat(DealtHoleCards);
	public IEnumerable<Card> BoardCards => GivenBoardCards.Concat(DealtBoardCards).Concat(ExtraBoardCards);
	public IEnumerable<Card> Cards => HoleCards.Concat(BoardCards);

	public string Name { get; private set; }

	public BaseballPlayer WithHoleCards(IReadOnlyCollection<Card> cards)
	{
		GivenHoleCards = cards;
		return this;
	}

	public BaseballPlayer WithBoardCards(IReadOnlyCollection<Card> cards)
	{
		GivenBoardCards = cards;
		return this;
	}
}