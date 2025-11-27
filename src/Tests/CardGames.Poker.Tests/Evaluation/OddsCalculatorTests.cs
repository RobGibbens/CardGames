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
        // Pocket Aces against 1 opponent preflop should have high win rate
        var heroCards = "As Ah".ToCards();
        var communityCards = new List<CardGames.Core.French.Cards.Card>();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 1,
            simulations: 500);

        // Pocket Aces preflop vs 1 opponent should win ~80%+
        result.WinProbability.Should().BeGreaterThan(0.75m);
        result.WinProbability.Should().BeLessThanOrEqualTo(1.0m);
        result.LoseProbability.Should().BeGreaterThanOrEqualTo(0.0m);
        result.TieProbability.Should().BeGreaterThanOrEqualTo(0.0m);
        
        // Sum of probabilities should be approximately 1
        var totalProb = result.WinProbability + result.TieProbability + result.LoseProbability;
        totalProb.Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_ReturnsValidOdds_WithFlop()
    {
        // Hero has top pair on a flop
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac 7h 2s".ToCards();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 1,
            simulations: 500);

        // Top pair with best kicker should have decent win rate
        result.WinProbability.Should().BeGreaterThan(0.5m);
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
            opponentCount: 1,
            simulations: 1000);

        // Hand type probabilities should include common types
        result.HandTypeProbabilities.Should().ContainKey(HandType.HighCard);
        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
    }

    [Fact]
    public void CalculateHoldemOdds_HandlesMultipleOpponents()
    {
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac Kh 2s".ToCards(); // Hero has two pair
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 5,
            simulations: 500);

        // With more opponents, win rate should decrease
        result.WinProbability.Should().BeGreaterThan(0.0m);
        result.WinProbability.Should().BeLessThan(1.0m);
        
        var totalProb = result.WinProbability + result.TieProbability + result.LoseProbability;
        totalProb.Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateDrawOdds_ReturnsValidOdds()
    {
        // A pair should beat high card most of the time
        var heroCards = "As Ah Kd Qc 2s".ToCards();
        
        var result = OddsCalculator.CalculateDrawOdds(
            heroCards,
            opponentCount: 1,
            simulations: 500);

        result.WinProbability.Should().BeGreaterThan(0.3m);
        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities[HandType.OnePair].Should().Be(1.0m); // Hero always has a pair
    }

    [Fact]
    public void CalculateStudOdds_ReturnsValidOdds()
    {
        // Hero has a pair of aces in hole
        var heroHoleCards = "As Ah".ToCards();
        var heroBoardCards = "Kd".ToCards();
        var opponentBoardCards = new List<IReadOnlyCollection<CardGames.Core.French.Cards.Card>>
        {
            "7c".ToCards()
        };
        
        var result = OddsCalculator.CalculateStudOdds(
            heroHoleCards,
            heroBoardCards,
            opponentBoardCards,
            simulations: 500);

        // Pair of aces should win often
        result.WinProbability.Should().BeGreaterThan(0.5m);
        
        var totalProb = result.WinProbability + result.TieProbability + result.LoseProbability;
        totalProb.Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateOmahaOdds_ReturnsValidOdds()
    {
        // Hero has good starting hand
        var heroHoleCards = "As Ad Ks Kd".ToCards();
        var communityCards = new List<CardGames.Core.French.Cards.Card>();
        
        var result = OddsCalculator.CalculateOmahaOdds(
            heroHoleCards,
            communityCards,
            opponentCount: 1,
            simulations: 500);

        // Double paired hand should do reasonably well
        result.WinProbability.Should().BeGreaterThan(0.3m);
        
        var totalProb = result.WinProbability + result.TieProbability + result.LoseProbability;
        totalProb.Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_ExpectedPotShare_IsReasonable()
    {
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac 7h 2s".ToCards();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 1,
            simulations: 500);

        // Expected pot share should be between 0 and 1
        result.ExpectedPotShare.Should().BeGreaterThanOrEqualTo(0.0m);
        result.ExpectedPotShare.Should().BeLessThanOrEqualTo(1.0m);
        
        // Expected pot share should be approximately equal to win + tie/2
        // (assuming ties split pot evenly)
        var expectedShare = result.WinProbability + (result.TieProbability * 0.5m);
        result.ExpectedPotShare.Should().BeApproximately(expectedShare, 0.1m);
    }

    [Fact]
    public void CalculateHoldemOdds_WithDeadCards_AdjustsCorrectly()
    {
        var heroCards = "As Ah".ToCards();
        var communityCards = "Kc 7h 2s".ToCards();
        // Dead cards include a pair of Queens (folded by opponent)
        var deadCards = "Qd Qc".ToCards();
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 1,
            deadCards: deadCards,
            simulations: 500);

        // With dead cards removed, probabilities should still sum to 1
        var totalProb = result.WinProbability + result.TieProbability + result.LoseProbability;
        totalProb.Should().BeApproximately(1.0m, 0.01m);
    }

    [Fact]
    public void CalculateHoldemOdds_OnRiver_HandTypeIs100Percent()
    {
        // On the river, the hand type is known with certainty
        var heroCards = "As Kd".ToCards();
        var communityCards = "Ac 7h 2s 3c 9d".ToCards(); // Full 5-card board
        
        var result = OddsCalculator.CalculateHoldemOdds(
            heroCards,
            communityCards,
            opponentCount: 1,
            simulations: 500);

        // Should have exactly one hand type with 100% probability
        result.HandTypeProbabilities.Values.Sum().Should().BeApproximately(1.0m, 0.01m);
        result.HandTypeProbabilities.Should().ContainKey(HandType.OnePair);
        result.HandTypeProbabilities[HandType.OnePair].Should().Be(1.0m);
    }
}
