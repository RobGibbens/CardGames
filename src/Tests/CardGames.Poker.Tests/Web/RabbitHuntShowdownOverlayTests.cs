#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class RabbitHuntShowdownOverlayTests
{
    [Fact]
    public void GetOrderedRabbitHuntCards_SortsByDealOrder()
    {
        var result = new GetRabbitHuntSuccessful
        {
            GameId = Guid.NewGuid(),
            HandNumber = 17,
            GameTypeCode = "HOLDEM",
            CommunityCards =
            [
                CreateRabbitHuntCard(5, CardSuit.Hearts, CardSymbol.Ace, dealtAtPhase: "River"),
                CreateRabbitHuntCard(1, CardSuit.Spades, CardSymbol.Deuce, dealtAtPhase: "Flop"),
                CreateRabbitHuntCard(4, CardSuit.Clubs, CardSymbol.King, dealtAtPhase: "Turn"),
                CreateRabbitHuntCard(2, CardSuit.Diamonds, CardSymbol.Three, dealtAtPhase: "Flop"),
                CreateRabbitHuntCard(3, CardSuit.Hearts, CardSymbol.Four, dealtAtPhase: "Flop")
            ],
            PreviouslyVisibleCards = [],
            NewlyRevealedCards = []
        };

        var orderedCards = InvokeOrderedRabbitHuntCards(result);

        orderedCards.Select(card => card.DealOrder).Should().Equal(1, 2, 3, 4, 5);
        orderedCards.Select(card => card.DealtAtPhase).Should().Equal("Flop", "Flop", "Flop", "Turn", "River");
    }

    private static IReadOnlyList<RabbitHuntCardDto> InvokeOrderedRabbitHuntCards(GetRabbitHuntSuccessful result)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetOrderedRabbitHuntCards",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay should normalize Rabbit Hunt cards before rendering");

        var orderedCards = method!.Invoke(null, [result]);
        orderedCards.Should().NotBeNull();

        return orderedCards!
            .Should()
            .BeAssignableTo<IReadOnlyList<RabbitHuntCardDto>>()
            .Subject;
    }

    private static RabbitHuntCardDto CreateRabbitHuntCard(
        int dealOrder,
        CardSuit suit,
        CardSymbol symbol,
        bool wasAlreadyVisible = false,
        string? dealtAtPhase = null)
    {
        return new RabbitHuntCardDto
        {
            Card = new ShowdownCard(suit, symbol),
            DealOrder = dealOrder,
            WasAlreadyVisible = wasAlreadyVisible,
            DealtAtPhase = dealtAtPhase,
            IsKlondikeCard = false
        };
    }
}