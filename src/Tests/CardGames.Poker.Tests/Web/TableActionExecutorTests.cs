using System.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Web.Services.TableActions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableActionExecutorTests
{
    private sealed record Notification(string Message, string Type, int DurationMs);

    private sealed class Harness
    {
        public int RefreshCount { get; private set; }
        public List<Notification> Notifications { get; } = new();
        public List<bool> BusyTransitions { get; } = new();
        public bool Busy { get; set; }

        public TableActionExecutor Create() => new(
            NullLogger.Instance,
            () => { RefreshCount++; return Task.CompletedTask; },
            (message, type, duration) =>
            {
                Notifications.Add(new Notification(message, type, duration));
                return Task.CompletedTask;
            });

        public TableActionOptions Options() => new()
        {
            IsBusy = () => Busy,
            SetBusy = busy =>
            {
                Busy = busy;
                BusyTransitions.Add(busy);
            },
        };
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyBusy_SkipsOperation()
    {
        var harness = new Harness { Busy = true };
        var executor = harness.Create();
        var invoked = false;

        await executor.ExecuteAsync(
            () => { invoked = true; return Task.FromResult(TableActionResult.Ok()); },
            harness.Options());

        invoked.Should().BeFalse();
        harness.BusyTransitions.Should().BeEmpty();
        harness.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Success_TogglesBusyAndRefreshes()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => Task.FromResult(TableActionResult.Ok()),
            new TableActionOptions
            {
                IsBusy = () => harness.Busy,
                SetBusy = busy => { harness.Busy = busy; harness.BusyTransitions.Add(busy); },
                RefreshOnStart = true,
                RefreshOnComplete = true,
            });

        harness.BusyTransitions.Should().Equal(true, false);
        harness.RefreshCount.Should().Be(2);
        harness.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Success_InvokesOnSuccessAndOptionalToast()
    {
        var harness = new Harness();
        var executor = harness.Create();
        string? captured = null;

        await executor.ExecuteAsync<string>(
            () => Task.FromResult(TableActionResult<string>.Ok("hello")),
            onSuccess: result => { captured = result.Content; return Task.CompletedTask; },
            new TableActionOptions { SuccessMessage = "Done!", ActionName = "demo" });

        captured.Should().Be("hello");
        harness.Notifications.Should().ContainSingle();
        harness.Notifications[0].Message.Should().Be("Done!");
        harness.Notifications[0].Type.Should().Be("info");
    }

    [Fact]
    public async Task ExecuteAsync_Failure_ShowsNormalizedError()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => Task.FromResult(TableActionResult.Fail("Not your turn")),
            new TableActionOptions { ShowFailureToast = true });

        harness.Notifications.Should().ContainSingle();
        harness.Notifications[0].Message.Should().Be("Not your turn");
        harness.Notifications[0].Type.Should().Be("error");
    }

    [Fact]
    public async Task ExecuteAsync_Failure_UsesFallbackWhenNoError()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => Task.FromResult(TableActionResult.Fail(null)),
            new TableActionOptions { FailureFallbackMessage = "Could not act." });

        harness.Notifications.Should().ContainSingle();
        harness.Notifications[0].Message.Should().Be("Could not act.");
    }

    [Fact]
    public async Task ExecuteAsync_Failure_FixedMessageOverridesError()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => Task.FromResult(TableActionResult.Fail("server detail")),
            new TableActionOptions { FailureMessage = "Friendly message" });

        harness.Notifications.Should().ContainSingle();
        harness.Notifications[0].Message.Should().Be("Friendly message");
    }

    [Fact]
    public async Task ExecuteAsync_Failure_SuppressesToastWhenDisabled()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => Task.FromResult(TableActionResult.Fail("ignored")),
            new TableActionOptions { ShowFailureToast = false });

        harness.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Exception_SwallowsByDefaultAndResetsBusy()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => throw new InvalidOperationException("boom"),
            harness.Options());

        harness.BusyTransitions.Should().Equal(true, false);
        harness.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Exception_ShowsToastWhenRequested()
    {
        var harness = new Harness();
        var executor = harness.Create();

        await executor.ExecuteAsync(
            () => throw new InvalidOperationException("boom"),
            new TableActionOptions { ShowExceptionToast = true, FailureFallbackMessage = "Try again." });

        harness.Notifications.Should().ContainSingle();
        harness.Notifications[0].Message.Should().Be("Try again.");
    }

    [Fact]
    public async Task ExecuteAsync_Exception_RethrowsWhenRequested_AfterResettingBusy()
    {
        var harness = new Harness();
        var executor = harness.Create();

        var act = () => executor.ExecuteAsync(
            () => throw new InvalidOperationException("boom"),
            new TableActionOptions
            {
                IsBusy = () => harness.Busy,
                SetBusy = busy => { harness.Busy = busy; harness.BusyTransitions.Add(busy); },
                RethrowOnException = true,
            });

        await act.Should().ThrowAsync<InvalidOperationException>();
        harness.BusyTransitions.Should().Equal(true, false);
        harness.Busy.Should().BeFalse();
    }
}
