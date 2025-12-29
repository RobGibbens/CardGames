using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Simulations.TwosJacksManWithTheAxe;

/// <summary>
/// Represents a player in a Twos, Jacks, Man with the Axe simulation.
/// </summary>
public class TwosJacksManWithTheAxePlayer
{
    public IReadOnlyCollection<Card> GivenCards { get; private set; } = Array.Empty<Card>();
    public IReadOnlyCollection<Card> DealtCards { get; set; } = Array.Empty<Card>();

    public IEnumerable<Card> Cards => GivenCards.Concat(DealtCards);

    public string Name { get; private set; }

    public TwosJacksManWithTheAxePlayer(string name)
    {
        Name = name;
    }

    public TwosJacksManWithTheAxePlayer WithCards(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count > 5)
        {
            throw new ArgumentException($"{Name} has too many cards to play Twos, Jacks, Man with the Axe.");
        }
        GivenCards = cards;
        return this;
    }
}
