using System;
using CardGames.Poker.Web.Utilities;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class LeagueTimeZoneHelperTests
{
	[Fact]
	public void TryConvertLocalToUtc_AndBack_RoundTripsIanaTimeZone()
	{
		var localDate = new DateTime(2026, 5, 1);
		var timeZoneId = "America/New_York";

		var converted = LeagueTimeZoneHelper.TryConvertLocalToUtc(localDate, "19:30", timeZoneId, out var utcValue, out var errorMessage);

		converted.Should().BeTrue(errorMessage);
		errorMessage.Should().BeNull();
		LeagueTimeZoneHelper.ConvertUtcToLocalDate(utcValue, timeZoneId).Should().Be(localDate);
		LeagueTimeZoneHelper.ConvertUtcToTimeText(utcValue, timeZoneId).Should().Be("19:30");
	}
}