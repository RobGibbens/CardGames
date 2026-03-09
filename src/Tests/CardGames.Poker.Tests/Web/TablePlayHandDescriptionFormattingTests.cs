using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayHandDescriptionFormattingTests
{
    [Theory]
    [InlineData("13-12-9-7-5 low", "King-Queen-9-7-5 low")]
    [InlineData("12-11-6-5-2", "Queen-Jack-6-5-2")]
    [InlineData("14-13-12-11-10 low", "Ace-King-Queen-Jack-10 low")]
    [InlineData("1-13-12-11-10 low", "Ace-King-Queen-Jack-10 low")]
    [InlineData("Pair of Sixes", "Pair of Sixes")]
    [InlineData("", "")]
    public void FormatHandDescriptionForDisplay_FormatsFaceCards_WhenLowDescription(string input, string expected)
    {
        var method = typeof(TablePlay).GetMethod(
            "FormatHandDescriptionForDisplay",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay.FormatHandDescriptionForDisplay should exist");

        var result = method!.Invoke(null, [input]);
        result.Should().Be(expected);
    }
}
