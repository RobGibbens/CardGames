#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services.Cache;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.Cache;

public class ActiveGameCacheEvictionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CallsCompactOnSchedule()
    {
        var options = Options.Create(new ActiveGameCacheOptions
        {
            Enabled = true,
            ScavengeInterval = TimeSpan.FromMilliseconds(50),
            MaxSnapshotAge = TimeSpan.FromMinutes(30)
        });

        var cache = Substitute.For<IActiveGameCache>();
        var timeProvider = TimeProvider.System;
        var logger = Substitute.For<ILogger<ActiveGameCacheEvictionService>>();

        var svc = new ActiveGameCacheEvictionService(cache, Substitute.For<IGameStateManager>(), options, Options.Create(new InMemoryEngineOptions()), timeProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            await svc.StartAsync(cts.Token);
            await Task.Delay(150, CancellationToken.None);
            await svc.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        cache.Received().Compact(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ExecuteAsync_DisabledCache_DoesNotCompact()
    {
        var options = Options.Create(new ActiveGameCacheOptions
        {
            Enabled = false,
            ScavengeInterval = TimeSpan.FromMilliseconds(50)
        });

        var cache = Substitute.For<IActiveGameCache>();
        var timeProvider = TimeProvider.System;
        var logger = Substitute.For<ILogger<ActiveGameCacheEvictionService>>();

        var svc = new ActiveGameCacheEvictionService(cache, Substitute.For<IGameStateManager>(), options, Options.Create(new InMemoryEngineOptions()), timeProvider, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            await svc.StartAsync(cts.Token);
            await Task.Delay(150, CancellationToken.None);
            await svc.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        cache.DidNotReceive().Compact(Arg.Any<DateTimeOffset>());
    }
}
