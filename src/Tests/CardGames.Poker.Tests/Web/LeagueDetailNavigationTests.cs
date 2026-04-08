using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class LeagueDetailNavigationTests
{
	[Theory]
	[InlineData("/table/11111111-1111-1111-1111-111111111111", "/table/11111111-1111-1111-1111-111111111111?autojoin=1")]
	[InlineData("/table/11111111-1111-1111-1111-111111111111?foo=bar", "/table/11111111-1111-1111-1111-111111111111?foo=bar&autojoin=1")]
	[InlineData("/table/11111111-1111-1111-1111-111111111111?autojoin=1", "/table/11111111-1111-1111-1111-111111111111?autojoin=1")]
	[InlineData("/table/11111111-1111-1111-1111-111111111111?AUTOJOIN=true", "/table/11111111-1111-1111-1111-111111111111?AUTOJOIN=true")]
	public void BuildLeagueTablePath_ReturnsExpectedResult(string input, string expected)
	{
		var result = InvokeBuildLeagueTablePath(input);

		result.Should().Be(expected);
	}

	private static string InvokeBuildLeagueTablePath(string tablePath)
	{
		var method = typeof(LeagueDetail).GetMethod("BuildLeagueTablePath", BindingFlags.Static | BindingFlags.NonPublic);
		method.Should().NotBeNull("BuildLeagueTablePath should exist on LeagueDetail");

		var result = method!.Invoke(null, new object?[] { tablePath });
		result.Should().BeOfType<string>();

		return (string)result!;
	}
}