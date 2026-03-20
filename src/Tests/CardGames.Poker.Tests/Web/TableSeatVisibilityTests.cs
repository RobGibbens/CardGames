using System.Reflection;
using CardGames.Poker.Web.Components.Shared;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableSeatVisibilityTests
{
    [Fact]
    public void ShouldShowRemoteCardFace_HidesPrivateCardPayload()
    {
        var privateCard = new CardInfo
        {
            Rank = "Q",
            Suit = "Spades",
            IsFaceUp = true,
            IsPubliclyVisible = false
        };

        InvokeShouldShowRemoteCardFace(privateCard).Should().BeFalse();
    }

    [Fact]
    public void ShouldShowRemoteCardFace_ShowsPublicCardPayload()
    {
        var publicCard = new CardInfo
        {
            Rank = "K",
            Suit = "Hearts",
            IsFaceUp = true,
            IsPubliclyVisible = true
        };

        InvokeShouldShowRemoteCardFace(publicCard).Should().BeTrue();
    }

    private static bool InvokeShouldShowRemoteCardFace(CardInfo card)
    {
        var method = typeof(TableSeat).GetMethod("ShouldShowRemoteCardFace", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull("TableSeat should keep remote seat visibility logic centralized");

        var result = method!.Invoke(null, [card]);
        result.Should().BeOfType<bool>();
        return (bool)result!;
    }
}