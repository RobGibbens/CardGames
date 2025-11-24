using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Simulations.Draw;

/// <summary>
/// Represents a player in a Five Card Draw simulation.
/// </summary>
public class FiveCardDrawPlayer
{
    public IReadOnlyCollection<Card> GivenCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtCards { get; set; } = Array.Empty<Card>();

    public IEnumerable<Card> Cards => GivenCards.Concat(DealtCards);

    public string Name { get; private set; }

    public FiveCardDrawPlayer(string name)
    {
        Name = name;
    }

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
