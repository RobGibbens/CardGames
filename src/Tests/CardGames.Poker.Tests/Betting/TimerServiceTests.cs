#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class TimerServiceTests
{
    [Fact]
    public void Constructor_WithDefaultConfig_InitializesCorrectly()
    {
        var service = new TimerService();

        service.Config.Should().NotBeNull();
        service.Config.DefaultTimeoutSeconds.Should().Be(30);
        service.IsRunning.Should().BeFalse();
        service.CurrentPlayerName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithCustomConfig_UsesProvidedConfig()
    {
        var config = new TurnTimerConfig
        {
            DefaultTimeoutSeconds = 45,
            WarningThresholdSeconds = 15,
            TimeBankSeconds = 120,
            AutoActOnTimeout = false
        };

        var service = new TimerService(config);

        service.Config.DefaultTimeoutSeconds.Should().Be(45);
        service.Config.WarningThresholdSeconds.Should().Be(15);
        service.Config.TimeBankSeconds.Should().Be(120);
        service.Config.AutoActOnTimeout.Should().BeFalse();
    }

    [Fact]
    public void StartTimer_WithValidPlayer_StartsTimer()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);

        service.StartTimer("Alice");

        service.IsRunning.Should().BeTrue();
        service.CurrentPlayerName.Should().Be("Alice");
        service.SecondsRemaining.Should().Be(30);
    }

    [Fact]
    public void StartTimer_WhenTimerDisabled_DoesNotStart()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 0 };
        var service = new TimerService(config);

        service.StartTimer("Alice");

        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void StartTimer_RaisesTimerStartedEvent()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 30, 
            TimeBankSeconds = 60 
        };
        var service = new TimerService(config);
        TimerStartedEventArgs? capturedEvent = null;
        service.OnTimerStarted += e => capturedEvent = e;

        service.StartTimer("Alice");

        capturedEvent.Should().NotBeNull();
        capturedEvent!.PlayerName.Should().Be("Alice");
        capturedEvent.DurationSeconds.Should().Be(30);
        capturedEvent.TimeBankRemaining.Should().Be(60);
    }

    [Fact]
    public void StopTimer_WhenRunning_StopsTimer()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);
        service.StartTimer("Alice");

        service.StopTimer();

        service.IsRunning.Should().BeFalse();
        service.CurrentPlayerName.Should().BeNull();
    }

    [Fact]
    public void ResetTimer_RestartsTimerForSamePlayer()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);
        service.StartTimer("Alice");
        
        // Wait a bit to let time elapse
        Thread.Sleep(100);

        service.ResetTimer();

        service.IsRunning.Should().BeTrue();
        service.CurrentPlayerName.Should().Be("Alice");
        service.SecondsRemaining.Should().Be(30);
    }

    [Fact]
    public void GetTimeBankRemaining_ForNewPlayer_ReturnsDefaultAmount()
    {
        var config = new TurnTimerConfig { TimeBankSeconds = 60 };
        var service = new TimerService(config);

        var remaining = service.GetTimeBankRemaining("Alice");

        remaining.Should().Be(60);
    }

    [Fact]
    public void InitializePlayerTimeBank_SetsTimeBankToConfigValue()
    {
        var config = new TurnTimerConfig { TimeBankSeconds = 90 };
        var service = new TimerService(config);
        
        service.InitializePlayerTimeBank("Alice");

        service.GetTimeBankRemaining("Alice").Should().Be(90);
    }

    [Fact]
    public void RemovePlayer_RemovesTimeBankTracking()
    {
        var config = new TurnTimerConfig { TimeBankSeconds = 60 };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");

        service.RemovePlayer("Alice");

        // After removal, player should get the default again
        service.GetTimeBankRemaining("Alice").Should().Be(60);
    }

    [Fact]
    public void UseTimeBank_WhenTimerRunning_AddsTimeAndReducesBank()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 10, 
            TimeBankSeconds = 30 
        };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");
        service.StartTimer("Alice");

        var result = service.UseTimeBank("Alice");

        result.Should().BeTrue();
        service.GetTimeBankRemaining("Alice").Should().Be(0);
    }

    [Fact]
    public void UseTimeBank_WhenNotRunning_ReturnsFalse()
    {
        var config = new TurnTimerConfig { TimeBankSeconds = 30 };
        var service = new TimerService(config);

        var result = service.UseTimeBank("Alice");

        result.Should().BeFalse();
    }

    [Fact]
    public void UseTimeBank_ForWrongPlayer_ReturnsFalse()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 30, 
            TimeBankSeconds = 60 
        };
        var service = new TimerService(config);
        service.StartTimer("Alice");

        var result = service.UseTimeBank("Bob");

        result.Should().BeFalse();
    }

    [Fact]
    public void UseTimeBank_WhenNoTimeBankLeft_ReturnsFalse()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 30, 
            TimeBankSeconds = 30 
        };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");
        service.StartTimer("Alice");
        service.UseTimeBank("Alice"); // Use all time bank

        service.StopTimer();
        service.StartTimer("Alice");

        var result = service.UseTimeBank("Alice");

        result.Should().BeFalse();
    }

    [Fact]
    public void UseTimeBank_RaisesTimeBankUsedEvent()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 10, 
            TimeBankSeconds = 30 
        };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");
        service.StartTimer("Alice");
        TimeBankUsedEventArgs? capturedEvent = null;
        service.OnTimeBankUsed += e => capturedEvent = e;

        service.UseTimeBank("Alice");

        capturedEvent.Should().NotBeNull();
        capturedEvent!.PlayerName.Should().Be("Alice");
        capturedEvent.SecondsAdded.Should().Be(30);
        capturedEvent.TimeBankRemaining.Should().Be(0);
    }

    [Fact]
    public void UseTimeBank_CanOnlyBeUsedOncePerTurn()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 10, 
            TimeBankSeconds = 60 
        };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");
        service.StartTimer("Alice");
        
        var firstResult = service.UseTimeBank("Alice");
        var secondResult = service.UseTimeBank("Alice");

        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
    }

    [Fact]
    public void ResetAllTimeBanks_ClearsAllPlayerTimeBanks()
    {
        var config = new TurnTimerConfig { TimeBankSeconds = 60 };
        var service = new TimerService(config);
        service.InitializePlayerTimeBank("Alice");
        service.InitializePlayerTimeBank("Bob");
        service.StartTimer("Alice");
        service.UseTimeBank("Alice");

        service.ResetAllTimeBanks();

        // After reset, players get the default again
        service.GetTimeBankRemaining("Alice").Should().Be(60);
        service.GetTimeBankRemaining("Bob").Should().Be(60);
    }

    [Fact]
    public async Task Timer_RaisesTickEvents()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 3 };
        var service = new TimerService(config);
        var tickEvents = new List<TimerTickEventArgs>();
        service.OnTimerTick += e => tickEvents.Add(e);

        service.StartTimer("Alice");
        
        // Wait for at least 2 ticks
        await Task.Delay(2500);
        
        service.StopTimer();

        tickEvents.Should().HaveCountGreaterThanOrEqualTo(2);
        tickEvents[0].PlayerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Timer_RaisesWarningEvent()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 2, 
            WarningThresholdSeconds = 2 
        };
        var service = new TimerService(config);
        TimerWarningEventArgs? warningEvent = null;
        service.OnTimerWarning += e => warningEvent = e;

        service.StartTimer("Alice");
        
        // Wait for warning
        await Task.Delay(1500);
        
        service.StopTimer();

        warningEvent.Should().NotBeNull();
        warningEvent!.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public async Task Timer_RaisesExpiredEvent_WhenTimeRunsOut()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 1,
            WarningThresholdSeconds = 1,
            AutoActOnTimeout = true 
        };
        var service = new TimerService(config, _ => BettingActionType.Check);
        TimerExpiredEventArgs? expiredEvent = null;
        service.OnTimerExpired += e => expiredEvent = e;

        service.StartTimer("Alice");
        
        // Wait for expiration
        await Task.Delay(1500);

        expiredEvent.Should().NotBeNull();
        expiredEvent!.PlayerName.Should().Be("Alice");
    }

    [Fact]
    public void Dispose_StopsTimer()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);
        service.StartTimer("Alice");

        service.Dispose();

        service.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void StartTimer_AfterDispose_ThrowsObjectDisposedException()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);
        service.Dispose();

        var act = () => service.StartTimer("Alice");

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void StartTimer_StopsPreviousTimerFirst()
    {
        var config = new TurnTimerConfig { DefaultTimeoutSeconds = 30 };
        var service = new TimerService(config);
        var startEvents = new List<TimerStartedEventArgs>();
        service.OnTimerStarted += e => startEvents.Add(e);

        service.StartTimer("Alice");
        service.StartTimer("Bob");

        startEvents.Should().HaveCount(2);
        service.CurrentPlayerName.Should().Be("Bob");
    }

    [Fact]
    public void DefaultActionProvider_IsUsedOnExpiration()
    {
        var config = new TurnTimerConfig 
        { 
            DefaultTimeoutSeconds = 1,
            AutoActOnTimeout = true 
        };
        
        // Custom provider that returns Check for all players
        BettingActionType actionProvider(string player) => BettingActionType.Check;
        
        var service = new TimerService(config, actionProvider);
        TimerExpiredEventArgs? expiredEvent = null;
        service.OnTimerExpired += e => expiredEvent = e;

        service.StartTimer("Alice");
        
        // Wait for expiration
        Thread.Sleep(1500);

        expiredEvent.Should().NotBeNull();
        expiredEvent!.DefaultAction.Should().Be(BettingActionType.Check);
    }
}
