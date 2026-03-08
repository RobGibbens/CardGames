using System.Collections.Generic;
using System.Reflection;
using CardGames.Poker.Web.Components.Shared;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableCanvasBlindIndicatorsTests
{
    [Fact]
    public void HoldTheBaseball_ThreeHanded_BlindIndicatorsResolveFromDealerPosition()
    {
        // Arrange: dealer seat 0 -> SB seat 1, BB seat 2
        var canvas = CreateTableCanvas(
            "HOLDTHEBASEBALL",
            0,
            [
                CreateOccupiedSeat(0),
                CreateOccupiedSeat(1),
                CreateOccupiedSeat(2)
            ]
        );

        // Act
        var smallBlindSeat = InvokePrivateSeatIndexMethod(canvas, "GetSmallBlindSeatIndex");
        var bigBlindSeat = InvokePrivateSeatIndexMethod(canvas, "GetBigBlindSeatIndex");

        // Assert
        smallBlindSeat.Should().Be(1);
        bigBlindSeat.Should().Be(2);
    }

    [Fact]
    public void HoldTheBaseball_HeadsUp_DealerIsSmallBlindAndOtherSeatIsBigBlind()
    {
        // Arrange: heads-up dealer seat 0 -> SB seat 0, BB seat 1
        var canvas = CreateTableCanvas(
            "HOLDTHEBASEBALL",
            0,
            [
                CreateOccupiedSeat(0),
                CreateOccupiedSeat(1)
            ]
        );

        // Act
        var smallBlindSeat = InvokePrivateSeatIndexMethod(canvas, "GetSmallBlindSeatIndex");
        var bigBlindSeat = InvokePrivateSeatIndexMethod(canvas, "GetBigBlindSeatIndex");

        // Assert
        smallBlindSeat.Should().Be(0);
        bigBlindSeat.Should().Be(1);
    }

    private static int InvokePrivateSeatIndexMethod(TableCanvas canvas, string methodName)
    {
        var method = typeof(TableCanvas).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on TableCanvas");

        var result = method!.Invoke(canvas, null);
        result.Should().BeOfType<int>();
        return (int)result!;
    }

    private static TableCanvas CreateTableCanvas(string gameTypeCode, int dealerSeatIndex, List<SeatInfo> seats)
    {
        var canvas = new TableCanvas();
        SetComponentParameter(canvas, nameof(TableCanvas.GameTypeCode), gameTypeCode);
        SetComponentParameter(canvas, nameof(TableCanvas.DealerSeatIndex), dealerSeatIndex);
        SetComponentParameter(canvas, nameof(TableCanvas.Seats), seats);
        return canvas;
    }

    private static void SetComponentParameter(TableCanvas component, string parameterName, object value)
    {
        var property = typeof(TableCanvas).GetProperty(parameterName, BindingFlags.Instance | BindingFlags.Public);
        property.Should().NotBeNull($"{parameterName} should exist on TableCanvas");
        property!.GetCustomAttribute<ParameterAttribute>()
            .Should().NotBeNull($"{parameterName} should be a [Parameter] on TableCanvas");
        property.SetValue(component, value);
    }

    private static SeatInfo CreateOccupiedSeat(int seatIndex) => new()
    {
        SeatIndex = seatIndex,
        IsOccupied = true,
        PlayerName = $"Player {seatIndex + 1}",
        Chips = 1000
    };
}
