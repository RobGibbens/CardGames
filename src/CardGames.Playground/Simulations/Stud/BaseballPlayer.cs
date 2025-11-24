using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Playground.Simulations.Stud;

/// <summary>
/// Baseball player that can have extra cards from receiving 4s face up.
/// In baseball, when a player is dealt a 4 face up, they receive an extra face up card.
/// </summary>
public class BaseballPlayer
{
    public IReadOnlyCollection<Card> GivenHoleCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtHoleCards { get; set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> GivenBoardCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtBoardCards { get; set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> ExtraBoardCards { get; set; } = Array.Empty<Card>();

    public IEnumerable<Card> HoleCards => GivenHoleCards.Concat(DealtHoleCards);
    public IEnumerable<Card> BoardCards => GivenBoardCards.Concat(DealtBoardCards).Concat(ExtraBoardCards);
    public IEnumerable<Card> Cards => HoleCards.Concat(BoardCards);

    public string Name { get; private set; }

    public BaseballPlayer(string name)
    {
        Name = name;
    }

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
