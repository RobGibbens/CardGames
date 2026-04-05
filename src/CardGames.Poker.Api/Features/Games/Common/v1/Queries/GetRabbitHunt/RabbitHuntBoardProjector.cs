using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

internal static class RabbitHuntBoardProjector
{
    public static bool SupportsRabbitHunt(string? gameTypeCode)
    {
        return string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.OmahaCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode, StringComparison.OrdinalIgnoreCase);
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
        {
            AddNextCard(projectedCards, remainingDeckCards, ref deckIndex, 0, "KlondikeCard", isKlondikeCard: true);
        }

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
                {
                    EnsureSequentialBoard(projectedCards, remainingDeckCards, ref deckIndex, 6, 1, "RedRiverBonus");
                }
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
            if (projectedCards.Any(card => card.DealOrder == dealOrder))
            {
                continue;
            }

            AddNextCard(projectedCards, remainingDeckCards, ref deckIndex, dealOrder, phase, isKlondikeCard: false);
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
        if (deckIndex >= remainingDeckCards.Count)
        {
            return;
        }

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

    private static CardGames.Poker.Api.Contracts.CardSuit MapSuit(CardGames.Poker.Api.Data.Entities.CardSuit suit)
    {
        return suit switch
        {
            CardGames.Poker.Api.Data.Entities.CardSuit.Hearts => CardGames.Poker.Api.Contracts.CardSuit.Hearts,
            CardGames.Poker.Api.Data.Entities.CardSuit.Diamonds => CardGames.Poker.Api.Contracts.CardSuit.Diamonds,
            CardGames.Poker.Api.Data.Entities.CardSuit.Spades => CardGames.Poker.Api.Contracts.CardSuit.Spades,
            CardGames.Poker.Api.Data.Entities.CardSuit.Clubs => CardGames.Poker.Api.Contracts.CardSuit.Clubs,
            _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown card suit")
        };
    }

    private static CardGames.Poker.Api.Contracts.CardSymbol MapSymbol(CardGames.Poker.Api.Data.Entities.CardSymbol symbol)
    {
        return symbol switch
        {
            CardGames.Poker.Api.Data.Entities.CardSymbol.Deuce => CardGames.Poker.Api.Contracts.CardSymbol.Deuce,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Three => CardGames.Poker.Api.Contracts.CardSymbol.Three,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Four => CardGames.Poker.Api.Contracts.CardSymbol.Four,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Five => CardGames.Poker.Api.Contracts.CardSymbol.Five,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Six => CardGames.Poker.Api.Contracts.CardSymbol.Six,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Seven => CardGames.Poker.Api.Contracts.CardSymbol.Seven,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Eight => CardGames.Poker.Api.Contracts.CardSymbol.Eight,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Nine => CardGames.Poker.Api.Contracts.CardSymbol.Nine,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Ten => CardGames.Poker.Api.Contracts.CardSymbol.Ten,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Jack => CardGames.Poker.Api.Contracts.CardSymbol.Jack,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Queen => CardGames.Poker.Api.Contracts.CardSymbol.Queen,
            CardGames.Poker.Api.Data.Entities.CardSymbol.King => CardGames.Poker.Api.Contracts.CardSymbol.King,
            CardGames.Poker.Api.Data.Entities.CardSymbol.Ace => CardGames.Poker.Api.Contracts.CardSymbol.Ace,
            _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown card symbol")
        };
    }

    private static bool IsRedSuit(CardGames.Poker.Api.Contracts.CardSuit? suit)
    {
        return suit is CardGames.Poker.Api.Contracts.CardSuit.Hearts or CardGames.Poker.Api.Contracts.CardSuit.Diamonds;
    }
}