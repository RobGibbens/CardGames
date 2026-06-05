using System.Linq;
using CardGames.Core.Dealer;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Decks;
using CardGames.Core.Random;

namespace CardGames.Core.French.Dealers;

public class FrenchDeckDealer : Dealer<Card>
{
	public FrenchDeckDealer(FrenchDeck deck)
		: base(deck)
	{
	}

	public FrenchDeckDealer(FrenchDeck deck, IRandomNumberGenerator numberGenerator)
		: base(deck, numberGenerator)
	{
	}

	private FrenchDeck SpecificDeck => Deck as FrenchDeck;

	public static FrenchDeckDealer WithShortDeck()
	{
		return new FrenchDeckDealer(new ShortFrenchDeck());
	}

	public static FrenchDeckDealer WithFullDeck()
	{
		return new FrenchDeckDealer(new FullFrenchDeck());
	}

	public bool TryDealCardOfValue(int value, out Card card)
	{
		var availableCards = SpecificDeck
			.CardsLeftOfValue(value)
			.ToArray();

		return TryDealCardFrom(availableCards, out card);
	}

	public bool TryDealCardOfSymbol(Symbol symbol, out Card card)
	{
		return TryDealCardOfValue((int)symbol, out card);
	}

	public bool TryDealCardOfSuit(Suit suit, out Card card)
	{
		var availableCards = SpecificDeck
			.CardsLeftOfSuit(suit)
			.ToArray();

		return TryDealCardFrom(availableCards, out card);
	}

	public Card DealSpecific(Card card)
	{
		return Deck.GetSpecific(card);
	}

	private bool TryDealCardFrom(Card[] cards, out Card? card)
	{
		if (!cards.Any())
		{
			card = null;
			return false;
		}

		card = cards[NumberGenerator.Next(cards.Length)];
		_ = Deck.GetSpecific(card);
		return true;
	}
}