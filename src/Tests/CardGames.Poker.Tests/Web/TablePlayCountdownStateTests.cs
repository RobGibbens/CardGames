using System;
using System.Threading;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayCountdownStateTests
{
    private static TableStatePublicDto BuildState(
        DateTimeOffset serverUtcNow,
        DateTimeOffset? nextHandStartsAtUtc = null,
        DateTimeOffset? handCompletedAtUtc = null)
    {
        return new TableStatePublicDto
        {
            GameId = Guid.NewGuid(),
            CurrentPhase = "Showdown",
            Seats = Array.Empty<SeatPublicDto>(),
            ServerUtcNow = serverUtcNow,
            NextHandStartsAtUtc = nextHandStartsAtUtc,
            HandCompletedAtUtc = handCompletedAtUtc,
        };
    }

    [Fact]
    public void New_State_Has_No_Deadline_And_Zero_Seconds()
    {
        var state = new TablePlayCountdownState();

        state.HasDeadline.Should().BeFalse();
        state.SecondsUntilNextHand.Should().Be(0);
    }

    [Fact]
    public void SyncWithServer_Uses_NextHandStartsAtUtc_As_Deadline()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;

        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(10)),
            isEndedPhase: false,
            isGameCompleted: false);

        state.HasDeadline.Should().BeTrue();
        state.SecondsUntilNextHand.Should().BeInRange(9, 10);
    }

    [Fact]
    public void SyncWithServer_Without_Deadline_While_Running_Clears_Countdown()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;

        state.SyncWithServer(
            BuildState(serverNow),
            isEndedPhase: false,
            isGameCompleted: false);

        state.HasDeadline.Should().BeFalse();
        state.SecondsUntilNextHand.Should().Be(0);
    }

    [Fact]
    public void SyncWithServer_When_Ended_Uses_HandCompleted_Plus_Return_Grace()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;

        state.SyncWithServer(
            BuildState(serverNow, handCompletedAtUtc: serverNow),
            isEndedPhase: true,
            isGameCompleted: false);

        state.HasDeadline.Should().BeTrue();
        state.SecondsUntilNextHand.Should()
            .BeInRange(
                TablePlayCountdownState.CompletedGameReturnToLobbyDurationSeconds - 1,
                TablePlayCountdownState.CompletedGameReturnToLobbyDurationSeconds);
    }

    [Fact]
    public void SyncWithServer_Prefers_NextHand_Over_Completed_Grace()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;

        state.SyncWithServer(
            BuildState(
                serverNow,
                nextHandStartsAtUtc: serverNow.AddSeconds(3),
                handCompletedAtUtc: serverNow),
            isEndedPhase: true,
            isGameCompleted: false);

        state.SecondsUntilNextHand.Should().BeInRange(2, 3);
    }

    [Fact]
    public void SyncWithServer_Applies_Server_Clock_Offset()
    {
        var state = new TablePlayCountdownState();
        // Server clock is 1 hour ahead of the client; the deadline is expressed in server time.
        var serverNow = DateTimeOffset.UtcNow.AddHours(1);

        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(8)),
            isEndedPhase: false,
            isGameCompleted: false);

        state.SecondsUntilNextHand.Should().BeInRange(7, 8);
    }

    [Fact]
    public void Past_Deadline_Yields_Zero_Seconds()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;

        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(-5)),
            isEndedPhase: false,
            isGameCompleted: false);

        state.HasDeadline.Should().BeTrue();
        state.SecondsUntilNextHand.Should().Be(0);
    }

    [Fact]
    public void Reset_Clears_Deadline_And_Seconds()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;
        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(10)),
            isEndedPhase: false,
            isGameCompleted: false);

        state.Reset();

        state.HasDeadline.Should().BeFalse();
        state.SecondsUntilNextHand.Should().Be(0);
    }

    [Fact]
    public void GetTimerDueTime_Is_Infinite_When_No_Deadline()
    {
        var state = new TablePlayCountdownState();

        state.GetTimerDueTime().Should().Be(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void GetTimerDueTime_Is_Within_One_Second_When_Deadline_Set()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;
        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(10)),
            isEndedPhase: false,
            isGameCompleted: false);

        var dueTime = state.GetTimerDueTime();

        dueTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        dueTime.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTimerDueTime_Is_Zero_When_Deadline_Passed()
    {
        var state = new TablePlayCountdownState();
        var serverNow = DateTimeOffset.UtcNow;
        state.SyncWithServer(
            BuildState(serverNow, nextHandStartsAtUtc: serverNow.AddSeconds(-5)),
            isEndedPhase: false,
            isGameCompleted: false);

        state.GetTimerDueTime().Should().Be(TimeSpan.Zero);
    }
}
