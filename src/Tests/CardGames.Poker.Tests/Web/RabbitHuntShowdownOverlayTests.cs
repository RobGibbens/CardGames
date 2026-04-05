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
            PlayerCards =
            [
                new ShowdownCard(CardSuit.Hearts, CardSymbol.Ace),
                new ShowdownCard(CardSuit.Spades, CardSymbol.King)
            ],
            CommunityCards =
            [
                CreateRabbitHuntCard(5, CardSuit.Hearts, CardSymbol.Ace, dealtAtPhase: "River"),
                CreateRabbitHuntCard(1, CardSuit.Spades, CardSymbol.Deuce, dealtAtPhase: "Flop"),
                CreateRabbitHuntCard(4, CardSuit.Clubs, CardSymbol.King, dealtAtPhase: "Turn"),
                CreateRabbitHuntCard(2, CardSuit.Diamonds, CardSymbol.Three, dealtAtPhase: "Flop"),
                CreateRabbitHuntCard(3, CardSuit.Hearts, CardSymbol.Four, dealtAtPhase: "Flop")
            ],
            PreviouslyVisibleCards = [],
            NewlyRevealedCards = [],
            ProjectedHandEvaluationDescription = null
        };

        var orderedCards = InvokeOrderedRabbitHuntCards(result);

        orderedCards.Select(card => card.DealOrder).Should().Equal(1, 2, 3, 4, 5);
        orderedCards.Select(card => card.DealtAtPhase).Should().Equal("Flop", "Flop", "Flop", "Turn", "River");
    }

    [Fact]
    public void GetRabbitHuntProjectedHandDescription_TrimsWhitespace()
    {
        var result = new GetRabbitHuntSuccessful
        {
            GameId = Guid.NewGuid(),
            HandNumber = 18,
            GameTypeCode = "OMAHA",
            PlayerCards =
            [
                new ShowdownCard(CardSuit.Hearts, CardSymbol.Ace),
                new ShowdownCard(CardSuit.Spades, CardSymbol.Ace),
                new ShowdownCard(CardSuit.Clubs, CardSymbol.King),
                new ShowdownCard(CardSuit.Diamonds, CardSymbol.King)
            ],
            CommunityCards = [],
            PreviouslyVisibleCards = [],
            NewlyRevealedCards = [],
            ProjectedHandEvaluationDescription = "  Straight to the Ace  "
        };

        var description = InvokeProjectedHandDescription(result);

        description.Should().Be("Straight to the Ace");
    }

    [Fact]
    public void GetRabbitHuntPlayerCards_ReturnsPlayerCardsInDisplayOrder()
    {
        var result = new GetRabbitHuntSuccessful
        {
            GameId = Guid.NewGuid(),
            HandNumber = 19,
            GameTypeCode = "IRISHHOLDEM",
            PlayerCards =
            [
                new ShowdownCard(CardSuit.Hearts, CardSymbol.Ace),
                new ShowdownCard(CardSuit.Hearts, CardSymbol.King),
                new ShowdownCard(CardSuit.Spades, CardSymbol.Queen),
                new ShowdownCard(CardSuit.Clubs, CardSymbol.Queen)
            ],
            CommunityCards = [],
            PreviouslyVisibleCards = [],
            NewlyRevealedCards = [],
            ProjectedHandEvaluationDescription = null
        };

        var playerCards = InvokeRabbitHuntPlayerCards(result);

        playerCards.Should().Equal(result.PlayerCards);
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

    private static string? InvokeProjectedHandDescription(GetRabbitHuntSuccessful result)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetRabbitHuntProjectedHandDescription",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay should normalize Rabbit Hunt hand descriptions before rendering");

        return method!.Invoke(null, [result])
            .Should()
            .BeAssignableTo<string>()
            .Subject;
    }

    private static IReadOnlyList<ShowdownCard> InvokeRabbitHuntPlayerCards(GetRabbitHuntSuccessful result)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetRabbitHuntPlayerCards",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay should provide the player's Rabbit Hunt hole cards for rendering");

        return method!.Invoke(null, [result])
            .Should()
            .BeAssignableTo<IReadOnlyList<ShowdownCard>>()
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