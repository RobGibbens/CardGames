using System.Collections.Generic;
using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayPlayerChipsTests
{
    [Fact]
    public void PlayerChips_ReflectsServerDrivenSeatChips()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_currentPlayerSeatIndex", 1);
        SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
        {
            new() { SeatIndex = 0, IsOccupied = true, PlayerName = "villain@example.com", Chips = 500 },
            new() { SeatIndex = 1, IsOccupied = true, PlayerName = "hero@example.com", Chips = 1234 }
        });

        GetPlayerChips(tablePlay).Should().Be(1234);

        // Authoritative server state lowers the seated player's stack; the UI should follow.
        var seats = GetPrivateField<List<SeatInfo>>(tablePlay, "_seats");
        seats[1].Chips = 750;

        GetPlayerChips(tablePlay).Should().Be(750);
    }

    [Fact]
    public void PlayerChips_IsZeroWhenNotSeated()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_currentPlayerSeatIndex", -1);
        SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
        {
            new() { SeatIndex = 0, IsOccupied = true, PlayerName = "villain@example.com", Chips = 500 }
        });

        GetPlayerChips(tablePlay).Should().Be(0);
    }

    private static int GetPlayerChips(TablePlay tablePlay)
    {
        var member = typeof(TablePlay).GetProperty("_playerChips", BindingFlags.Instance | BindingFlags.NonPublic);
        member.Should().NotBeNull("TablePlay should derive _playerChips from authoritative seat state");
        return (int)member!.GetValue(tablePlay)!;
    }

    private static void SetPrivateField<T>(TablePlay tablePlay, string fieldName, T value)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        field!.SetValue(tablePlay, value);
    }

    private static T GetPrivateField<T>(TablePlay tablePlay, string fieldName)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        var value = field!.GetValue(tablePlay);
        value.Should().BeOfType<T>();
        return (T)value!;
    }
}
