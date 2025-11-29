using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class FixedLimitStrategyTests
{
    [Fact]
    public void Constructor_SetsSmallBetByDefault()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        strategy.BettingIncrement.Should().Be(10);
    }

    [Fact]
    public void Constructor_WithUseBigBet_SetsBigBet()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20, useBigBet: true);

        strategy.BettingIncrement.Should().Be(20);
    }

    [Fact]
    public void GetMinBet_ReturnsBettingIncrement()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var minBet = strategy.GetMinBet(bigBlind: 10, currentBet: 0, lastRaiseAmount: 0);

        minBet.Should().Be(10);
    }

    [Fact]
    public void GetMaxBet_ReturnsBettingIncrement()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var maxBet = strategy.GetMaxBet(playerStack: 1000, currentPot: 100, currentBet: 0, playerCurrentBet: 0);

        maxBet.Should().Be(10);
    }

    [Fact]
    public void GetMaxBet_WhenStackLessThanIncrement_ReturnsStack()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var maxBet = strategy.GetMaxBet(playerStack: 5, currentPot: 100, currentBet: 0, playerCurrentBet: 0);

        maxBet.Should().Be(5);
    }

    [Fact]
    public void GetMinRaise_ReturnsCurrentBetPlusIncrement()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var minRaise = strategy.GetMinRaise(currentBet: 10, lastRaiseAmount: 10, bigBlind: 10);

        minRaise.Should().Be(20);
    }

    [Fact]
    public void GetMaxRaise_ReturnsCurrentBetPlusIncrement()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var maxRaise = strategy.GetMaxRaise(playerStack: 1000, currentPot: 100, currentBet: 10, playerCurrentBet: 0);

        maxRaise.Should().Be(20);
    }

    [Fact]
    public void IsValidBet_WithExactIncrement_ReturnsTrue()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var isValid = strategy.IsValidBet(amount: 10, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidBet_NotExactIncrement_ReturnsFalse()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var isValid = strategy.IsValidBet(amount: 15, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidBet_AllInForLess_ReturnsTrue()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var isValid = strategy.IsValidBet(amount: 5, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 5, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_WithExactIncrement_ReturnsTrue()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var isValid = strategy.IsValidRaise(totalAmount: 20, currentBet: 10, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 10, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_NotExactIncrement_ReturnsFalse()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var isValid = strategy.IsValidRaise(totalAmount: 25, currentBet: 10, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 10, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ForStage_CreatesNewStrategyWithBigBet()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20);

        var bigBetStrategy = strategy.ForStage(useBigBet: true);

        bigBetStrategy.BettingIncrement.Should().Be(20);
    }

    [Fact]
    public void ForStage_CreatesNewStrategyWithSmallBet()
    {
        var strategy = new FixedLimitStrategy(smallBet: 10, bigBet: 20, useBigBet: true);

        var smallBetStrategy = strategy.ForStage(useBigBet: false);

        smallBetStrategy.BettingIncrement.Should().Be(10);
    }
}
