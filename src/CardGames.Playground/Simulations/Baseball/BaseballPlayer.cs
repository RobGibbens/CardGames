using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;

namespace CardGames.Playground.Simulations.Baseball;

/// <summary>
/// Represents a player in a Baseball poker game.
/// Baseball is a variant of seven card stud where 3s and 9s are wild,
/// and 4s dealt face-up give the player an extra face-up card.
/// </summary>
public class BaseballPlayer
{
    public string Name { get; }

    /// <summary>
    /// Hole cards (face down cards) given to the player at setup.
    /// In standard Baseball, player receives 2 hole cards initially.
    /// </summary>
    public IReadOnlyCollection<Card> GivenHoleCards { get; private set; } = Array.Empty<Card>();

    /// <summary>
    /// Hole cards dealt to the player during simulation.
    /// </summary>
    public IReadOnlyCollection<Card> DealtHoleCards { get; set; } = Array.Empty<Card>();

    /// <summary>
    /// Open cards (face up cards) given to the player at setup.
    /// </summary>
    public IReadOnlyCollection<Card> GivenOpenCards { get; private set; } = Array.Empty<Card>();

    /// <summary>
    /// Open cards dealt to the player during simulation.
    /// This may include extra cards from 4s being dealt.
    /// </summary>
    public IReadOnlyCollection<Card> DealtOpenCards { get; set; } = Array.Empty<Card>();

    /// <summary>
    /// The final down card given at setup.
    /// </summary>
    public Card GivenDownCard { get; private set; }

    /// <summary>
    /// The final down card dealt during simulation.
    /// </summary>
    public Card DealtDownCard { get; set; }

    /// <summary>
    /// All hole cards (initial face down cards).
    /// </summary>
    public IEnumerable<Card> HoleCards => GivenHoleCards.Concat(DealtHoleCards);

    /// <summary>
    /// All open cards (face up cards).
    /// </summary>
    public IEnumerable<Card> OpenCards => GivenOpenCards.Concat(DealtOpenCards);

    /// <summary>
    /// The final down card.
    /// </summary>
    public Card DownCard => GivenDownCard ?? DealtDownCard;

    /// <summary>
    /// All cards held by the player.
    /// </summary>
    public IEnumerable<Card> Cards
    {
        get
        {
            var cards = HoleCards.Concat(OpenCards);
            return DownCard != null ? cards.Append(DownCard) : cards;
        }
    }

    public BaseballPlayer(string name)
    {
        Name = name;
    }

    public BaseballPlayer WithHoleCards(IReadOnlyCollection<Card> cards)
    {
        GivenHoleCards = cards;
        return this;
    }

    public BaseballPlayer WithOpenCards(IReadOnlyCollection<Card> cards)
    {
        GivenOpenCards = cards;
        return this;
    }

    public BaseballPlayer WithDownCard(Card card)
    {
        GivenDownCard = card;
        return this;
    }
}
