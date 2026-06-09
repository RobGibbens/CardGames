using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.Deck;
using CardGames.Core.French.Cards;

namespace CardGames.Core.French.Decks;

public abstract class FrenchDeck : IDeck<Card>
{
	private readonly List<Card> _cardsOut = [];

	private Card this[int index]
		=> CardsLeftInternal()
			.ToArray()[index];

	protected abstract IReadOnlyCollection<Card> Cards { get; }

	public int NumberOfCardsLeft()
	{
		return Cards.Count - _cardsOut.Count;
	}

	public Card GetSpecific(Card card)
	{
		if (!Cards.Contains(card)) throw new ArgumentException($"The card {card} is not part of the deck at all!");

		if (_cardsOut.Contains(card)) throw new ArgumentException($"The card {card} has already been dealt!");

		_cardsOut.Add(card);
		return card;
	}

	public Card GetFromRemaining(int index)
	{
		var card = this[index];
		_cardsOut.Add(card);
		return card;
	}

	public void Reset()
	{
		_cardsOut.Clear();
	}

	private IEnumerable<Card> CardsLeftInternal()
	{
		return Cards.Except(_cardsOut);
	}

	public IReadOnlyCollection<Card> CardsLeft()
	{
		return CardsLeftInternal()
			.ToList();
	}

	public IReadOnlyCollection<Card> CardsLeftOfValue(int value)
	{
		return CardsLeftWith(card => card.Value == value);
	}

	public IReadOnlyCollection<Card> CardsLeftOfSuit(Suit suit)
	{
		return CardsLeftWith(card => card.Suit == suit);
	}

	public IReadOnlyCollection<Card> CardsLeftWith(Func<Card, bool> predicate)
	{
		return CardsLeftInternal()
			.Where(predicate)
			.ToList();
	}
}