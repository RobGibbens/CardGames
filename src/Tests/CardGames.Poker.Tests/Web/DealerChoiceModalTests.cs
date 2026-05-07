#nullable enable

using System;
using System.Reflection;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class DealerChoiceModalTests
{
	[Fact]
	public void IsBlindBasedGame_BobBarker_ReturnsTrue()
	{
		var method = typeof(DealerChoiceModal).GetMethod(
			"IsBlindBasedGame",
			BindingFlags.Static | BindingFlags.NonPublic);

		method.Should().NotBeNull("DealerChoiceModal should classify blind-based variants for Dealer's Choice submissions");

		var result = method!.Invoke(null, ["BOBBARKER"]);

		result.Should().BeOfType<bool>();
		((bool)result!).Should().BeTrue("Bob Barker uses blinds and must submit small and big blind values");
	}
}
