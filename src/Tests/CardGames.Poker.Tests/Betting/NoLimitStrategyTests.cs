using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class NoLimitStrategyTests
{
    private readonly NoLimitStrategy _strategy = new();

    [Fact]
    public void GetMinBet_ReturnsMinBet()
    {
        var minBet = _strategy.GetMinBet(bigBlind: 10, currentBet: 0, lastRaiseAmount: 0);

        minBet.Should().Be(10);
    }

    [Fact]
    public void GetMaxBet_ReturnsPlayerStack()
    {
        var maxBet = _strategy.GetMaxBet(playerStack: 1000, currentPot: 100, currentBet: 0, playerCurrentBet: 0);

        maxBet.Should().Be(1000);
    }

    [Fact]
    public void GetMinRaise_WithNoRaise_ReturnsBetPlusBigBlind()
    {
        var minRaise = _strategy.GetMinRaise(currentBet: 10, lastRaiseAmount: 0, bigBlind: 10);

        minRaise.Should().Be(20);
    }

    [Fact]
    public void GetMinRaise_WithPreviousRaise_ReturnsBetPlusLastRaise()
    {
        var minRaise = _strategy.GetMinRaise(currentBet: 30, lastRaiseAmount: 20, bigBlind: 10);

        minRaise.Should().Be(50);
    }

    [Fact]
    public void GetMaxRaise_ReturnsPlayerStackPlusCurrentBet()
    {
        var maxRaise = _strategy.GetMaxRaise(playerStack: 1000, currentPot: 100, currentBet: 20, playerCurrentBet: 0);

        maxRaise.Should().Be(1000);
    }

    [Fact]
    public void IsValidBet_WithValidAmount_ReturnsTrue()
    {
        var isValid = _strategy.IsValidBet(amount: 20, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidBet_WhenBetExists_ReturnsFalse()
    {
        var isValid = _strategy.IsValidBet(amount: 20, bigBlind: 10, currentBet: 10, lastRaiseAmount: 0, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidBet_BelowMinBet_ReturnsFalse()
    {
        var isValid = _strategy.IsValidBet(amount: 5, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidBet_AllInBelowMinBet_ReturnsTrue()
    {
        var isValid = _strategy.IsValidBet(amount: 5, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 5, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_WithValidAmount_ReturnsTrue()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 40, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 20, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_WhenNoBet_ReturnsFalse()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 20, currentBet: 0, lastRaiseAmount: 0, bigBlind: 10, playerStack: 1000, currentPot: 0, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidRaise_BelowMinRaise_ReturnsFalse()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 25, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 20, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidRaise_AllInBelowMinRaise_ReturnsTrue()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 25, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 25, currentPot: 20, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }
}
