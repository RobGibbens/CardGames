using CardGames.Poker.Api.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Betting;

public class FixedLimitStudBettingTests
{
	[Theory]
	[InlineData(5, 10, 0)]
	[InlineData(10, 10, 10)]
	[InlineData(14, 10, 10)]
	[InlineData(0, 10, 0)]
	[InlineData(20, 20, 20)]
	public void LastFullLevel_IsTheGreatestMultipleAtOrBelowTheStreetBet(
		int streetCurrentBet,
		int streetLimit,
		int expected)
	{
		FixedLimitStudBetting.LastFullLevel(streetCurrentBet, streetLimit).Should().Be(expected);
	}

	[Theory]
	[InlineData(5, 0, 1000, 10, 10)]
	[InlineData(10, 0, 1000, 10, 20)]
	[InlineData(14, 0, 1000, 10, 20)]
	[InlineData(0, 0, 4, 10, 4)]
	[InlineData(10, 10, 4, 10, 14)]
	[InlineData(5, 5, 1000, 10, 10)]
	[InlineData(5, 5, 0, 10, null)]
	[InlineData(10, 5, 5, 10, null)]
	public void LegalAggressiveTotal_IsTheOneFixedLimitTotalOrTheShortStack(
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int streetLimit,
		int? expected)
	{
		FixedLimitStudBetting
			.LegalAggressiveTotal(streetCurrentBet, playerCurrentBet, chipStack, streetLimit)
			.Should()
			.Be(expected);
	}
}
