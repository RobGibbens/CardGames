using CardGames.Poker.Api.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Betting;

public class BettingActionMathTests
{
	[Theory]
	[InlineData(1000, 2, 2, 0, 100, true, "stack")]
	[InlineData(5, 20, 20, 0, 5, true, "current bet")]
	[InlineData(30, 20, 20, 0, 100, true, "at least 40")]
	[InlineData(50, 10, 10, 0, 50, false, null)]
	[InlineData(25, 20, 20, 0, 25, false, null)]
	[InlineData(40, 20, 20, 0, 100, false, null)]
	[InlineData(60, 20, 10, 10, 40, true, "stack")]
	[InlineData(50, 20, 10, 10, 40, false, null)]
	public void ValidateRaise_MatchesStackAndStreetRules(
		int amount,
		int streetCurrentBet,
		int lastRaiseAmount,
		int playerCurrentBet,
		int chipStack,
		bool expectError,
		string? messageFragment)
	{
		var message = BettingActionMath.ValidateRaise(
			amount,
			streetCurrentBet,
			lastRaiseAmount,
			playerCurrentBet,
			chipStack);

		if (expectError)
		{
			message.Should().NotBeNull();
			message.Should().Contain(messageFragment);
		}
		else
		{
			message.Should().BeNull();
		}
	}

	[Theory]
	[InlineData(50, 10, 0, 50, 50, 50, 40)]
	[InlineData(25, 20, 0, 25, 25, 25, 5)]
	[InlineData(40, 20, 0, 100, 40, 40, 20)]
	[InlineData(50, 20, 10, 40, 50, 40, 30)]
	[InlineData(1000, 2, 0, 100, 100, 100, 98)]
	public void ComputeRaise_CapsChipsAtStack(
		int amount,
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int expectedTotal,
		int expectedChips,
		int expectedIncrement)
	{
		var (raiseTotal, chipsMoved, raiseIncrement) = BettingActionMath.ComputeRaise(
			amount,
			streetCurrentBet,
			playerCurrentBet,
			chipStack);

		raiseTotal.Should().Be(expectedTotal);
		chipsMoved.Should().Be(expectedChips);
		raiseIncrement.Should().Be(expectedIncrement);
		chipsMoved.Should().BeLessThanOrEqualTo(chipStack);
		raiseTotal.Should().Be(playerCurrentBet + chipsMoved);
	}
}
