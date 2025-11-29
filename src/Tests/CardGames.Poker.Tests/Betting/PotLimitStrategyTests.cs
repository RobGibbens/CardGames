using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class PotLimitStrategyTests
{
    private readonly PotLimitStrategy _strategy = new();

    [Fact]
    public void GetMinBet_ReturnsBigBlind()
    {
        var minBet = _strategy.GetMinBet(bigBlind: 10, currentBet: 0, lastRaiseAmount: 0);

        minBet.Should().Be(10);
    }

    [Fact]
    public void GetMaxBet_ReturnsPotSize()
    {
        var maxBet = _strategy.GetMaxBet(playerStack: 1000, currentPot: 100, currentBet: 0, playerCurrentBet: 0);

        maxBet.Should().Be(100);
    }

    [Fact]
    public void GetMaxBet_WhenStackLessThanPot_ReturnsStack()
    {
        var maxBet = _strategy.GetMaxBet(playerStack: 50, currentPot: 100, currentBet: 0, playerCurrentBet: 0);

        maxBet.Should().Be(50);
    }

    [Fact]
    public void GetMinRaise_ReturnsBetPlusMinimumRaise()
    {
        var minRaise = _strategy.GetMinRaise(currentBet: 10, lastRaiseAmount: 0, bigBlind: 10);

        minRaise.Should().Be(20);
    }

    [Fact]
    public void GetMaxRaise_ReturnsPotLimitedRaise()
    {
        // Pot = 100, current bet = 20, player current bet = 0
        // Amount to call = 20
        // Pot after call = 100 + 20 = 120
        // Max raise = current bet (20) + pot after call (120) = 140
        var maxRaise = _strategy.GetMaxRaise(playerStack: 1000, currentPot: 100, currentBet: 20, playerCurrentBet: 0);

        maxRaise.Should().Be(140);
    }

    [Fact]
    public void GetMaxRaise_WhenStackLimits_ReturnsStackPlusCurrentBet()
    {
        var maxRaise = _strategy.GetMaxRaise(playerStack: 50, currentPot: 100, currentBet: 20, playerCurrentBet: 0);

        maxRaise.Should().Be(50);
    }

    [Fact]
    public void IsValidBet_WithValidAmount_ReturnsTrue()
    {
        var isValid = _strategy.IsValidBet(amount: 50, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidBet_AbovePotLimit_ReturnsFalse()
    {
        var isValid = _strategy.IsValidBet(amount: 150, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 1000, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidBet_AllInAbovePotLimit_ReturnsTrue()
    {
        // Player stack is 150, pot is 100, so all-in should be valid even though > pot
        var isValid = _strategy.IsValidBet(amount: 150, bigBlind: 10, currentBet: 0, lastRaiseAmount: 0, playerStack: 150, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_WithValidAmount_ReturnsTrue()
    {
        // Pot = 100, current bet = 20, player current bet = 0
        // Max raise should be around 140
        var isValid = _strategy.IsValidRaise(totalAmount: 100, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidRaise_AbovePotLimit_ReturnsFalse()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 200, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 1000, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValidRaise_AllInAbovePotLimit_ReturnsTrue()
    {
        var isValid = _strategy.IsValidRaise(totalAmount: 200, currentBet: 20, lastRaiseAmount: 10, bigBlind: 10, playerStack: 200, currentPot: 100, playerCurrentBet: 0);

        isValid.Should().BeTrue();
    }
}
