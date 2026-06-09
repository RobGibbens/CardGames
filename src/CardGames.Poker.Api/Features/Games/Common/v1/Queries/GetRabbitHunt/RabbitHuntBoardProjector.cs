using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardSuit = CardGames.Poker.Api.Contracts.CardSuit;
using CardSymbol = CardGames.Poker.Api.Contracts.CardSymbol;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

internal static class RabbitHuntBoardProjector
{
	public static bool SupportsRabbitHunt(string? gameTypeCode)
	{
		return string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode, StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.OmahaCode, StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode,
			       StringComparison.OrdinalIgnoreCase)
		       || string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode,
			       StringComparison.OrdinalIgnoreCase);
	}

	public static IReadOnlyList<RabbitHuntCardDto> ProjectBoard(
		string gameTypeCode,
		IReadOnlyList<GameCard> currentCommunityCards,
		IReadOnlyList<GameCard> remainingDeckCards)
	{
		var projectedCards = currentCommunityCards
			.OrderBy(card => card.DealOrder)
			.Select(card => new RabbitHuntCardDto
			{
				Card = CreateShowdownCard(card),
				DealOrder = card.DealOrder,
				WasAlreadyVisible = card.IsVisible,
				IsKlondikeCard = string.Equals(card.DealtAtPhase, "KlondikeCard", StringComparison.OrdinalIgnoreCase),
				DealtAtPhase = card.DealtAtPhase
			})
			.ToList();

		var deckIndex = 0;

		if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase)
		    && projectedCards.All(card => !card.IsKlondikeCard))
			AddNextCard(projectedCards, remainingDeckCards, ref deckIndex, 0, "KlondikeCard", true);

		if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode, StringComparison.OrdinalIgnoreCase))
		{
			EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 1, 2, "Flop");
			EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 3, 1, "Turn");
		}
		else
		{
			EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 1, 3, "Flop");
			EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 4, 1, "Turn");
			EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 5, 1, "River");

			if (string.Equals(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode, StringComparison.OrdinalIgnoreCase))
			{
				var riverCard = projectedCards.FirstOrDefault(card => card.DealOrder == 5);
				if (riverCard is not null && IsRedSuit(riverCard.Card.Suit))
					EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 6, 1, "RedRiverBonus");
			}
		}

		return projectedCards
			.OrderBy(card => card.DealOrder)
			.ToList();
	}

	private static void EnsureSequentialBoard(
		List<RabbitHuntCardDto> projectedCards,
		IReadOnlyList<GameCard> remainingDeckCards,
		ref int deckIndex,
		int startDealOrder,
		int count,
		string phase)
	{
		for (var offset = 0; offset < count; offset++)
		{
			var dealOrder = startDealOrder + offset;
			if (projectedCards.Any(card => card.DealOrder == dealOrder)) continue;

			AddNextCard(projectedCards, remainingDeckCards, ref deckIndex, dealOrder, phase, false);
		}
	}

	private static void AddNextCard(
		List<RabbitHuntCardDto> projectedCards,
		IReadOnlyList<GameCard> remainingDeckCards,
		ref int deckIndex,
		int dealOrder,
		string phase,
		bool isKlondikeCard)
	{
		if (deckIndex >= remainingDeckCards.Count) return;

		var nextCard = remainingDeckCards[deckIndex++];
		projectedCards.Add(new RabbitHuntCardDto
		{
			Card = CreateShowdownCard(nextCard),
			DealOrder = dealOrder,
			WasAlreadyVisible = false,
			IsKlondikeCard = isKlondikeCard,
			DealtAtPhase = phase
		});
	}

	private static ShowdownCard CreateShowdownCard(GameCard card)
	{
		return new ShowdownCard(MapSuit(card.Suit), MapSymbol(card.Symbol));
	}

	private static CardSuit MapSuit(Data.Entities.CardSuit suit)
	{
		return suit switch
		{
			Data.Entities.CardSuit.Hearts => CardSuit.Hearts,
			Data.Entities.CardSuit.Diamonds => CardSuit.Diamonds,
			Data.Entities.CardSuit.Spades => CardSuit.Spades,
			Data.Entities.CardSuit.Clubs => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown card suit")
		};
	}

	private static CardSymbol MapSymbol(Data.Entities.CardSymbol symbol)
	{
		return symbol switch
		{
			Data.Entities.CardSymbol.Deuce => CardSymbol.Deuce,
			Data.Entities.CardSymbol.Three => CardSymbol.Three,
			Data.Entities.CardSymbol.Four => CardSymbol.Four,
			Data.Entities.CardSymbol.Five => CardSymbol.Five,
			Data.Entities.CardSymbol.Six => CardSymbol.Six,
			Data.Entities.CardSymbol.Seven => CardSymbol.Seven,
			Data.Entities.CardSymbol.Eight => CardSymbol.Eight,
			Data.Entities.CardSymbol.Nine => CardSymbol.Nine,
			Data.Entities.CardSymbol.Ten => CardSymbol.Ten,
			Data.Entities.CardSymbol.Jack => CardSymbol.Jack,
			Data.Entities.CardSymbol.Queen => CardSymbol.Queen,
			Data.Entities.CardSymbol.King => CardSymbol.King,
			Data.Entities.CardSymbol.Ace => CardSymbol.Ace,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown card symbol")
		};
	}

	private static bool IsRedSuit(CardSuit? suit)
	{
		return suit is CardSuit.Hearts or CardSuit.Diamonds;
	}
}