using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Decks;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class DashboardHandOddsCalculatorTests
{
    [Fact]
    public void Calculate_HoldEm_WithFlopPairAlreadyMade_DoesNotReturnHighCard()
    {
        var playerCards = "8c Kh".ToCards();
        var communityCards = "7d Kc Jc".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("HOLDEM", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities.Should().NotContainKey(HandType.HighCard);
    }

    [Fact]
    public void Calculate_GoodBadUgly_WithVisibleCommunityPairAlreadyMade_DoesNotReturnHighCard()
    {
        var playerCards = "8c Kh 2d 3s".ToCards();
        var communityCards = "7d Kc".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("GOODBADUGLY", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities.Should().NotContainKey(HandType.HighCard);
    }

    [Fact]
    public void Calculate_Nebraska_PreFlopWithFiveHoleCards_ReturnsValidOdds()
    {
        var playerCards = "As Ad Ks Kd Qh".ToCards();
        var communityCards = "".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("NEBRASKA", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void Calculate_SouthDakota_PreFlopWithFiveHoleCards_ReturnsValidOdds()
    {
        var playerCards = "As Ad Ks Kd Qh".ToCards();
        var communityCards = "".ToCards();

        var result = DashboardHandOddsCalculator.Calculate("SOUTHDAKOTA", playerCards, communityCards, []);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void Calculate_PairPressure_WithCompleteHand_UsesActiveWildRanksFromFaceUpCards()
    {
        var playerCards = "5h Kd As Ah 2c 3d Ad".ToCards();
        var faceUpCardsInOrder = "8h 8d 5c 5s Kh Kc".ToCards();

        var result = DashboardHandOddsCalculator.Calculate(
            "PAIRPRESSURE",
            playerCards,
            [],
            [],
            faceUpCardsInOrder);

        result.Should().NotBeNull();
        result!.HandTypeProbabilities.Should().ContainSingle();
        result.HandTypeProbabilities.Should().ContainKey(HandType.FiveOfAKind);
        result.HandTypeProbabilities[HandType.FiveOfAKind].Should().Be(1m);
    }

    [Fact]
    public void PairPressure_TableAwareSimulation_CanRotateWildRanksAway_AndProduceHighCard()
    {
        var heroCards = "Js Qs 6s 5c Kc".ToCards();
        var currentFaceUpCardsInOrder = "6s 8h 8d 5c 5d 9h Kc Kd Ah".ToCards();
        var pairPressurePlayers = new[]
        {
            new OddsCalculator.PairPressurePlayerSnapshot
            {
                SeatIndex = 1,
                TotalCardsDealt = 5,
                FaceUpCardsInOrder = "8h 5d Kd".ToCards(),
                ReceivesFutureCards = true
            },
            new OddsCalculator.PairPressurePlayerSnapshot
            {
                SeatIndex = 2,
                TotalCardsDealt = 5,
                FaceUpCardsInOrder = "8d 9h Ah".ToCards(),
                ReceivesFutureCards = true
            }
        };

        var unknownPool = "9c 9d 9s Ac Ad As 2c 3d 4c 7d".ToCards();
        var allKnownCards = heroCards
            .Concat(pairPressurePlayers.SelectMany(player => player.FaceUpCardsInOrder))
            .Concat(unknownPool)
            .ToHashSet();

        var deadCards = new FullFrenchDeck()
            .CardsLeft()
            .Where(card => !allKnownCards.Contains(card))
            .ToList();

        var fixedWildResult = OddsCalculator.CalculatePairPressureOdds(
            heroCards.Take(2).ToList(),
            heroCards.Skip(2).ToList(),
            currentFaceUpCardsInOrder,
            deadCards: deadCards,
            simulations: 5000);

        fixedWildResult.HandTypeProbabilities.Should().NotContainKey(HandType.HighCard);

        var tableAwareResult = DashboardHandOddsCalculator.Calculate(
            "PAIRPRESSURE",
            heroCards,
            [],
            deadCards,
            currentFaceUpCardsInOrder,
            pairPressurePlayers,
            heroSeatIndex: 0,
            dealerSeatIndex: 2);

        tableAwareResult.Should().NotBeNull();
        tableAwareResult!.HandTypeProbabilities.Should().ContainKey(HandType.HighCard);
        tableAwareResult.HandTypeProbabilities[HandType.HighCard].Should().BeGreaterThan(0m);
    }
}
