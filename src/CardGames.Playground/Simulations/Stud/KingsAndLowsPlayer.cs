using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;

namespace CardGames.Playground.Simulations.Stud;

public class KingsAndLowsPlayer
{
    public IReadOnlyCollection<Card> GivenHoleCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtHoleCards { get; set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> GivenBoardCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtBoardCards { get; set; } = Array.Empty<Card>();
    public bool LastCardFaceUp { get; set; } = false;

    public IEnumerable<Card> HoleCards => System.Linq.Enumerable.Concat(GivenHoleCards, DealtHoleCards);
    public IEnumerable<Card> BoardCards => System.Linq.Enumerable.Concat(GivenBoardCards, DealtBoardCards);
    public IEnumerable<Card> Cards => System.Linq.Enumerable.Concat(HoleCards, BoardCards);

    public string Name { get; private set; }

    public KingsAndLowsPlayer(string name)
    {
        Name = name;
    }

    public KingsAndLowsPlayer WithHoleCards(IReadOnlyCollection<Card> cards)
    {
        GivenHoleCards = cards;
        return this;
    }

    public KingsAndLowsPlayer WithBoardCards(IReadOnlyCollection<Card> cards)
    {
        GivenBoardCards = cards;
        return this;
    }

    public KingsAndLowsPlayer WithLastCardFaceUp(bool faceUp = true)
    {
        LastCardFaceUp = faceUp;
        return this;
    }
}
