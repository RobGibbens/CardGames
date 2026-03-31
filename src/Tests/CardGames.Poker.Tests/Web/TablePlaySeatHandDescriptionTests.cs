using System.Collections.Generic;
using System.Reflection;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlaySeatHandDescriptionTests
{
    [Fact]
    public void UpdateSeatsFromPublicState_UsesLatestPublicHandDescription()
    {
        var tablePlay = new TablePlay();
        SetLogger(tablePlay);
        SetPrivateField(tablePlay, "_currentPlayerSeatIndex", 0);
        SetPrivateField(tablePlay, "_loggedInUserEmail", "hero@example.com");
        SetPrivateField(tablePlay, "_currentPlayerName", "hero@example.com");
        SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
        {
            new()
            {
                SeatIndex = 0,
                IsOccupied = true,
                PlayerName = "hero@example.com",
                Chips = 1000,
                HandEvaluationDescription = "Two pair, Kings and Queens"
            }
        });

        InvokeUpdateSeatsFromPublicState(tablePlay,
        [
            new SeatPublicDto
            {
                SeatIndex = 0,
                IsOccupied = true,
                PlayerName = "hero@example.com",
                Chips = 1000,
                Cards = [],
                HandEvaluationDescription = "Two pair, Kings and Sixes"
            }
        ]);

        var seats = GetPrivateField<List<SeatInfo>>(tablePlay, "_seats");
        seats[0].HandEvaluationDescription.Should().Be("Two pair, Kings and Sixes");
    }

    private static void InvokeUpdateSeatsFromPublicState(TablePlay tablePlay, IReadOnlyList<SeatPublicDto> publicSeats)
    {
        var method = typeof(TablePlay).GetMethod("UpdateSeatsFromPublicState", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should update seat state from public SignalR snapshots");
        method!.Invoke(tablePlay, [publicSeats]);
    }

    private static void SetLogger(TablePlay tablePlay)
    {
        var property = typeof(TablePlay).GetProperty("Logger", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull("TablePlay should expose the injected logger property");
        property!.SetValue(tablePlay, NullLogger<TablePlay>.Instance);
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