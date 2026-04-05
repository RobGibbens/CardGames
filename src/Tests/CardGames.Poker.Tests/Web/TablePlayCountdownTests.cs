using System;
using System.Collections.Generic;
using System.Reflection;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayCountdownTests
{
    [Fact]
    public void CalculateSecondsUntilDeadline_UsesServerClockOffsetAndCeiling()
    {
        var deadlineUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 10, TimeSpan.Zero);
        var clientUtcNow = new DateTimeOffset(2026, 4, 5, 12, 0, 0, 750, TimeSpan.Zero);
        var serverClockOffset = TimeSpan.FromMilliseconds(1500);

        var result = InvokeCalculateSecondsUntilDeadline(deadlineUtc, clientUtcNow, serverClockOffset);

        result.Should().Be(8);
    }

    [Fact]
    public void ResolveCountdownDeadlineUtc_PrefersNextHandStartWhenPresent()
    {
        var handCompletedAtUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var nextHandStartsAtUtc = handCompletedAtUtc.AddSeconds(10);
        var state = CreateTableState(handCompletedAtUtc, nextHandStartsAtUtc);

        var result = InvokeResolveCountdownDeadlineUtc(state, isEndedPhase: true, isGameCompleted: true);

        result.Should().Be(nextHandStartsAtUtc);
    }

    [Fact]
    public void ResolveCountdownDeadlineUtc_UsesCompletedHandTimeForEndedGames()
    {
        var handCompletedAtUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var state = CreateTableState(handCompletedAtUtc, nextHandStartsAtUtc: null);

        var result = InvokeResolveCountdownDeadlineUtc(state, isEndedPhase: true, isGameCompleted: false);

        result.Should().Be(handCompletedAtUtc.AddSeconds(10));
    }

    private static int InvokeCalculateSecondsUntilDeadline(
        DateTimeOffset? deadlineUtc,
        DateTimeOffset clientUtcNow,
        TimeSpan serverClockOffset)
    {
        var method = typeof(TablePlay).GetMethod("CalculateSecondsUntilDeadline", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should calculate countdown seconds from the server-authored deadline");

        var result = method!.Invoke(null, [deadlineUtc, clientUtcNow, serverClockOffset]);
        result.Should().BeOfType<int>();
        return (int)result!;
    }

    private static DateTimeOffset? InvokeResolveCountdownDeadlineUtc(
        TableStatePublicDto state,
        bool isEndedPhase,
        bool isGameCompleted)
    {
        var method = typeof(TablePlay).GetMethod("ResolveCountdownDeadlineUtc", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should resolve countdown deadlines from shared server timestamps");

        var result = method!.Invoke(null, [state, isEndedPhase, isGameCompleted]);
        return result.Should().BeAssignableTo<DateTimeOffset?>().Subject;
    }

    private static TableStatePublicDto CreateTableState(DateTimeOffset? handCompletedAtUtc, DateTimeOffset? nextHandStartsAtUtc)
    {
        return new TableStatePublicDto
        {
            GameId = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Seats = new List<SeatPublicDto>(),
            HandCompletedAtUtc = handCompletedAtUtc,
            NextHandStartsAtUtc = nextHandStartsAtUtc,
            ServerUtcNow = handCompletedAtUtc ?? DateTimeOffset.UtcNow
        };
    }
}