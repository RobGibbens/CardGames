using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayAutoSeatJoinTests
{
    [Fact]
    public void TryGetAutoJoinSeatIndex_ReturnsLowestAvailableSeat_WhenMultipleSeatsOpen()
    {
        var tablePlay = CreateTablePlayWithSeats(
            CreateSeat(seatIndex: 3, isOccupied: false),
            CreateSeat(seatIndex: 0, isOccupied: true),
            CreateSeat(seatIndex: 4, isOccupied: false),
            CreateSeat(seatIndex: 1, isOccupied: true),
            CreateSeat(seatIndex: 2, isOccupied: true));

        var result = InvokeTryGetAutoJoinSeatIndex(tablePlay);

        result.Found.Should().BeTrue();
        result.SeatIndex.Should().Be(3);
    }

    [Fact]
    public void TryGetAutoJoinSeatIndex_ReturnsFalseAndMinusOne_WhenNoSeatsAvailable()
    {
        var tablePlay = CreateTablePlayWithSeats(
            CreateSeat(seatIndex: 0, isOccupied: true),
            CreateSeat(seatIndex: 1, isOccupied: true),
            CreateSeat(seatIndex: 2, isOccupied: true));

        var result = InvokeTryGetAutoJoinSeatIndex(tablePlay);

        result.Found.Should().BeFalse();
        result.SeatIndex.Should().Be(-1);
    }

    [Theory]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111?autojoin=1", true)]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111?autojoin=true", true)]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111?AUTOJOIN=1", true)]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111?autojoin=0", false)]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111", false)]
    [InlineData("https://localhost/table/11111111-1111-1111-1111-111111111111?foo=bar", false)]
    [InlineData("", false)]
    public void HasAutoJoinIntent_ReturnsExpectedResult(string uri, bool expected)
    {
        var result = InvokeHasAutoJoinIntent(uri);

        result.Should().Be(expected);
    }

    private static (bool Found, int SeatIndex) InvokeTryGetAutoJoinSeatIndex(TablePlay tablePlay)
    {
        var method = typeof(TablePlay).GetMethod("TryGetAutoJoinSeatIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("TryGetAutoJoinSeatIndex should exist on TablePlay");

        var args = new object[] { -1 };
        var found = method!.Invoke(tablePlay, args);

        found.Should().BeOfType<bool>();
        args[0].Should().BeOfType<int>();

        return ((bool)found!, (int)args[0]);
    }

    private static bool InvokeHasAutoJoinIntent(string uri)
    {
        var method = typeof(TablePlay).GetMethod("HasAutoJoinIntent", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull("HasAutoJoinIntent should exist on TablePlay");

        var result = method!.Invoke(null, new object[] { uri });
        result.Should().BeOfType<bool>();

        return (bool)result!;
    }

    private static TablePlay CreateTablePlayWithSeats(params SeatInfo[] seats)
    {
        var tablePlay = new TablePlay();
        var seatsField = typeof(TablePlay).GetField("_seats", BindingFlags.Instance | BindingFlags.NonPublic);
        seatsField.Should().NotBeNull("TablePlay should keep seat state in _seats");

        seatsField!.SetValue(tablePlay, seats.ToList());
        return tablePlay;
    }

    private static SeatInfo CreateSeat(int seatIndex, bool isOccupied) => new()
    {
        SeatIndex = seatIndex,
        IsOccupied = isOccupied,
        PlayerName = isOccupied ? $"Player {seatIndex + 1}" : null,
        Chips = isOccupied ? 1000 : 0
    };
}
