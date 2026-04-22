using System.Reflection;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class ShowdownOverlayGameOverTests
{
    [Fact]
    public void GetOverlayTitle_ReturnsGameOver_WhenGameEnded()
    {
        var overlay = new ShowdownOverlay
        {
            IsGameEnded = true
        };

        InvokeGetOverlayTitle(overlay).Should().Be("Game Over");
    }

    [Fact]
    public void GetOverlayTitle_ReturnsShowdown_WhenHandStillInProgress()
    {
        var overlay = new ShowdownOverlay
        {
            IsGameEnded = false
        };

        InvokeGetOverlayTitle(overlay).Should().Be("Showdown");
    }

    private static string InvokeGetOverlayTitle(ShowdownOverlay overlay)
    {
        var method = typeof(ShowdownOverlay).GetMethod("GetOverlayTitle", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("ShowdownOverlay should compute its title from the terminal game state");

        var result = method!.Invoke(overlay, null);
        result.Should().BeOfType<string>();
        return (string)result!;
    }
}
