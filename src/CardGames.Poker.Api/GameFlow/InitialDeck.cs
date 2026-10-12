using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Testing;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
///     Builds the 52-card order persisted at the start of a hand.
///     A script registered for the game replaces the shuffle once.
/// </summary>
public static class InitialDeck
{
	public static List<(CardSuit Suit, CardSymbol Symbol)> Create(Guid gameId)
	{
		if (ScriptedDeckStore.Instance.TryConsume(gameId, out var scripted))
			return scripted.ToList();

		return CreateRandom();
	}

	public static List<(CardSuit Suit, CardSymbol Symbol)> CreateRandom()
	{
		var dealer = FrenchDeckDealer.WithFullDeck();
		dealer.Shuffle();

		return dealer.DealCards(52)
			.Select(card => ((CardSuit)(int)card.Suit, (CardSymbol)(int)card.Symbol))
			.ToList();
	}

	public static IReadOnlyList<Card> CreateCards(Guid gameId)
	{
		return Create(gameId)
			.Select(card => new Card((Suit)(int)card.Suit, (Symbol)(int)card.Symbol))
			.ToList();
	}
}
