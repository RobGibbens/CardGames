using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Simulations.Stud;

public class StudPlayer
{
	public StudPlayer(string name)
	{
		Name = name;
	}

	public IReadOnlyCollection<Card> GivenHoleCards { get; private set; } = [];
	public IReadOnlyCollection<Card> DealtHoleCards { get; set; } = [];
	public IReadOnlyCollection<Card> GivenBoardCards { get; private set; } = [];
	public IReadOnlyCollection<Card> DealtBoardCards { get; set; } = [];

	public IEnumerable<Card> HoleCards => GivenHoleCards.Concat(DealtHoleCards);
	public IEnumerable<Card> BoardCards => GivenBoardCards.Concat(DealtBoardCards);
	public IEnumerable<Card> Cards => HoleCards.Concat(BoardCards);

	public string Name { get; private set; }

	public StudPlayer WithHoleCards(IReadOnlyCollection<Card> cards)
	{
		GivenHoleCards = cards;
		return this;
	}

	public StudPlayer WithBoardCards(IReadOnlyCollection<Card> cards)
	{
		GivenBoardCards = cards;
		return this;
	}
}