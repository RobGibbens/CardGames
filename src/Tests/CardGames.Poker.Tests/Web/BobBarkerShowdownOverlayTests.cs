using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class BobBarkerShowdownOverlayTests
{
    [Fact]
    public void GetBobBarkerPlayers_OrdersShowcaseRowsByWinnerThenPayoutThenName()
    {
        var overlay = new ShowdownOverlay
        {
            GameTypeCode = "BOBBARKER",
            ShowdownState = new ShowdownPublicDto
            {
                PlayerResults =
                [
                    CreatePlayer("zoe@example.com", showcaseAmountWon: 10, isShowcaseWinner: false, showcaseCardValue: 9),
                    CreatePlayer("amy@example.com", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10),
                    CreatePlayer("ben@example.com", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10),
                    CreatePlayer("nina@example.com", showcaseAmountWon: 0, isShowcaseWinner: false, showcaseCard: null, showcaseCardValue: null)
                ]
            }
        };

        var players = InvokeBobBarkerPlayers(overlay).ToList();

        players.Select(player => player.PlayerName).Should().Equal(
            "amy@example.com",
            "ben@example.com",
            "zoe@example.com");
    }

    [Fact]
    public void GetPlayerDisplayName_PrefersFirstNameAndFallsBackToEmailLocalPart()
    {
        var namedPlayer = CreatePlayer("amy@example.com", playerFirstName: "Amy", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10);
        var unnamedPlayer = CreatePlayer("zoe.turner@example.com", showcaseAmountWon: 10, isShowcaseWinner: false, showcaseCardValue: 9);

        InvokePlayerDisplayName(namedPlayer).Should().Be("Amy");
        InvokePlayerDisplayName(unnamedPlayer).Should().Be("Zoe");
    }

    private static IEnumerable<ShowdownPlayerResultDto> InvokeBobBarkerPlayers(ShowdownOverlay overlay)
    {
        var method = typeof(ShowdownOverlay).GetMethod("GetBobBarkerPlayers", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetBobBarkerPlayers should exist for Bob Barker rendering");

        var result = method!.Invoke(overlay, null);
        result.Should().NotBeNull();
        return result.Should().BeAssignableTo<IEnumerable<ShowdownPlayerResultDto>>().Subject;
    }

    private static string InvokePlayerDisplayName(ShowdownPlayerResultDto player)
    {
        var method = typeof(ShowdownOverlay).GetMethod("GetPlayerDisplayName", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetPlayerDisplayName should exist for Bob Barker rendering");

        var result = method!.Invoke(null, [player]);
        result.Should().BeOfType<string>();
        return (string)result!;
    }

    private static ShowdownPlayerResultDto CreatePlayer(
        string playerName,
        string? playerFirstName = null,
        int showcaseAmountWon = 0,
        bool isShowcaseWinner = false,
        int? showcaseCardValue = null,
        CardPublicDto? showcaseCard = null)
    {
        return new ShowdownPlayerResultDto
        {
            PlayerName = playerName,
            PlayerFirstName = playerFirstName,
            Cards = [],
            ShowcaseAmountWon = showcaseAmountWon,
            IsShowcaseWinner = isShowcaseWinner,
            ShowcaseCardValue = showcaseCardValue,
            ShowcaseCard = showcaseCard ?? (showcaseCardValue.HasValue
                ? new CardPublicDto
                {
                    IsFaceUp = true,
                    Rank = showcaseCardValue.Value == 14 ? "A" : showcaseCardValue.Value.ToString(),
                    Suit = "Spades",
                    DealOrder = 5
                }
                : null)
        };
    }
}