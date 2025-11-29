using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class LimitStrategyFactoryTests
{
    [Fact]
    public void Create_NoLimit_ReturnsNoLimitStrategy()
    {
        var strategy = LimitStrategyFactory.Create(LimitType.NoLimit);

        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void Create_PotLimit_ReturnsPotLimitStrategy()
    {
        var strategy = LimitStrategyFactory.Create(LimitType.PotLimit);

        strategy.Should().BeOfType<PotLimitStrategy>();
    }

    [Fact]
    public void Create_FixedLimit_ReturnsFixedLimitStrategy()
    {
        var strategy = LimitStrategyFactory.Create(LimitType.FixedLimit, smallBet: 10, bigBet: 20);

        strategy.Should().BeOfType<FixedLimitStrategy>();
    }

    [Fact]
    public void Create_FixedLimitWithBigBet_UsesBigBet()
    {
        var strategy = LimitStrategyFactory.Create(LimitType.FixedLimit, smallBet: 10, bigBet: 20, useBigBet: true);

        strategy.Should().BeOfType<FixedLimitStrategy>();
        ((FixedLimitStrategy)strategy).BettingIncrement.Should().Be(20);
    }

    [Fact]
    public void CreateNoLimit_ReturnsNoLimitStrategy()
    {
        var strategy = LimitStrategyFactory.CreateNoLimit();

        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void CreatePotLimit_ReturnsPotLimitStrategy()
    {
        var strategy = LimitStrategyFactory.CreatePotLimit();

        strategy.Should().BeOfType<PotLimitStrategy>();
    }

    [Fact]
    public void CreateFixedLimit_ReturnsFixedLimitStrategy()
    {
        var strategy = LimitStrategyFactory.CreateFixedLimit(smallBet: 10, bigBet: 20);

        strategy.Should().BeOfType<FixedLimitStrategy>();
    }
}
