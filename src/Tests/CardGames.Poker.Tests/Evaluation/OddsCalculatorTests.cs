using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class OddsCalculatorTests
{
    [Fact]
    public void CalculateHoldemOdds_ReturnsValidOdds_ForPocketAces()
    {
        var heroCards = "As Ah".ToCards();
        var communityCards = new List<CardGames.Core.French.Cards.Card>();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            simulations: 500);

        // Hand type probabilities should be valid
        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_ReturnsValidOdds_WithFlop()
    {
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac 7h 2s".ToCards();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
    }

    [Fact]
    public void CalculateHoldemOdds_ReturnsAllHandTypes()
    {
        var heroCards = "9s 8s".ToCards(); // Suited connectors - can make many hand types
        var communityCards = new List<CardGames.Core.French.Cards.Card>();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            simulations: 1000);

        // Hand type probabilities should include common types
        result.HandTypeProbabilities.Should().ContainKey(HandType.HighCard);
        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
    }

    [Fact]
    public void CalculateDrawOdds_ReturnsValidOdds()
    {
        var heroCards = "As Ah Kd Qc 2s".ToCards();
        
        var result = OddsCalculator.CalculateDrawOdds(
            heroCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities[HandType.OnePair].Should().Be(1.0m); // Hero always has a pair
    }

    [Fact]
    public void CalculateStudOdds_ReturnsValidOdds()
    {
        var heroHoleCards = "As Ah".ToCards();
        var heroBoardCards = "Kd".ToCards();
        
        var result = OddsCalculator.CalculateStudOdds(
            heroHoleCards,
            heroBoardCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateOmahaOdds_ReturnsValidOdds()
    {
        var heroHoleCards = "As Ad Ks Kd".ToCards();
        var communityCards = new List<CardGames.Core.French.Cards.Card>();
        
        var result = OddsCalculator.CalculateOmahaOdds(
            heroHoleCards,
            communityCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_WithDeadCards_AdjustsCorrectly()
    {
        var heroCards = "As Ah".ToCards();
        var communityCards = "Kc 7h 2s".ToCards();
        var deadCards = "Qd Qc".ToCards();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            deadCards,
            simulations: 500);

        // Probabilities should still sum to 1
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_OnRiver_HandTypeIs100Percent()
    {
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac 7h 2s 3c 9d".ToCards(); // Full 5-card board
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            simulations: 500);

        // Should have exactly one hand type with 100% probability
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities[HandType.OnePair].Should().Be(1.0m);
    }

    [Fact]
    public void CalculateBaseballOdds_ReturnsValidOdds()
    {
        // Baseball hand with a 3 (wild) in it
        var heroHoleCards = "3s Ah".ToCards();
        var heroBoardCards = "Kd".ToCards();
        
        var result = OddsCalculator.CalculateBaseballOdds(
            heroHoleCards,
            heroBoardCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateFollowTheQueenOdds_ReturnsValidOdds()
    {
        var heroHoleCards = "Qs Ah".ToCards(); // Queen is wild
        var heroBoardCards = "Kd".ToCards();
        var faceUpCards = "Kd 7c 5h".ToCards();
        
        var result = OddsCalculator.CalculateFollowTheQueenOdds(
            heroHoleCards,
            heroBoardCards,
            faceUpCards,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateKingsAndLowsOdds_ReturnsValidOdds()
    {
        // Kings are wild, plus lowest card (2) is wild
        var heroCards = "Ks 2h 7d 9c Jh".ToCards();
        
        var result = OddsCalculator.CalculateKingsAndLowsOdds(
            heroCards,
            kingRequired: false,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateKingsAndLowsOdds_WithKingRequired_ReturnsValidOdds()
    {
        // Only Kings are wild when kingRequired and no king in hand
        var heroCards = "As 2h 7d 9c Jh".ToCards();
        
        var result = OddsCalculator.CalculateKingsAndLowsOdds(
            heroCards,
            kingRequired: true,
            simulations: 500);

        result.HandTypeProbabilities.Should().NotBeEmpty();
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
    }
}
