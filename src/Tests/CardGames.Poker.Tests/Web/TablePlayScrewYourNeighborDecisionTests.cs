using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayScrewYourNeighborDecisionTests
{
    [Theory]
    [InlineData("Trade", "Traded")]
    [InlineData("Traded", "Traded")]
    [InlineData("Keep", "Kept")]
    [InlineData("Kept", "Kept")]
    [InlineData("Keep or Trade", null)]
    [InlineData("KeepOrTrade", null)]
    public void GetScrewYourNeighborDecisionDescription_OnlyMatchesExplicitDecisionOutcomes(string input, string? expected)
    {
        var method = typeof(TablePlay).GetMethod(
            "GetScrewYourNeighborDecisionDescription",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should normalize Screw Your Neighbor decision descriptions");

        var result = method!.Invoke(null, [input]);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Trade", true)]
    [InlineData("Traded", true)]
    [InlineData("Keep", false)]
    [InlineData("Kept", false)]
    [InlineData("Keep or Trade", false)]
    [InlineData("KeepOrTrade", false)]
    public void IsScrewYourNeighborTradeAction_OnlyReturnsTrueForExplicitTradeOutcomes(string input, bool expected)
    {
        var method = typeof(TablePlay).GetMethod(
            "IsScrewYourNeighborTradeAction",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should only animate Screw Your Neighbor card swaps for actual trade outcomes");

        var result = method!.Invoke(null, [input]);
        result.Should().Be(expected);
    }
}