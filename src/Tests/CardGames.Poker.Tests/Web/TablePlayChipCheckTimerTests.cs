using System;
using System.Reflection;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayChipCheckTimerTests
{
    [Fact]
    public void GetChipCheckSecondsRemaining_PrefersSignalRTimerWhilePaused()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_isPausedForChipCheck", true);
        SetPrivateField(tablePlay, "_chipCheckPauseEndsAt", DateTimeOffset.UtcNow.AddMinutes(5));
        SetPrivateField(tablePlay, "_actionTimerState", new ActionTimerStateDto
        {
            SecondsRemaining = 173,
            DurationSeconds = 300,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-127),
            PlayerSeatIndex = -1,
            IsActive = true
        });

        InvokeGetChipCheckSecondsRemaining(tablePlay).Should().Be(173);
    }

    [Fact]
    public void GetChipCheckSecondsRemaining_FallsBackToDeadlineWithoutSignalRTimer()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_isPausedForChipCheck", true);
        SetPrivateField(tablePlay, "_chipCheckPauseEndsAt", DateTimeOffset.UtcNow.AddSeconds(42));
        SetPrivateField<ActionTimerStateDto?>(tablePlay, "_actionTimerState", null);

        InvokeGetChipCheckSecondsRemaining(tablePlay).Should().BeInRange(40, 42);
    }

    private static int InvokeGetChipCheckSecondsRemaining(TablePlay tablePlay)
    {
        var method = typeof(TablePlay).GetMethod("GetChipCheckSecondsRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should keep chip-check countdown calculation internal to the component");

        var value = method!.Invoke(tablePlay, null);
        value.Should().BeOfType<int>();
        return (int)value!;
    }

    private static void SetPrivateField<T>(TablePlay tablePlay, string fieldName, T value)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        field!.SetValue(tablePlay, value);
    }
}
