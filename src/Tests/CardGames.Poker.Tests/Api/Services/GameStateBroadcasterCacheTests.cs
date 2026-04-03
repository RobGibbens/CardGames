#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.Cache;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameStateBroadcasterCacheTests
{
    // ── StoreSnapshot via BroadcastGameStateAsync ──────────────────────

    [Fact]
    public async Task BroadcastGameStateAsync_CacheEnabled_StoresSnapshotInCache()
    {
        var gameId = Guid.NewGuid();
        var userId = "alice@example.com";
        var privateState = CreatePrivateState(0);
        var publicState = CreatePublicState(gameId);
        var result = CreateBroadcastResult(publicState, userId, privateState, version: 5);
        var cache = CreateRealCache(enabled: true);

        var sut = CreateBroadcaster(
            buildResult: result, cache: cache,
            cacheEnabled: true, serveReconnects: true);

        await sut.BroadcastGameStateAsync(gameId);

        cache.TryGet(gameId, out var snapshot).Should().BeTrue();
        snapshot.VersionNumber.Should().Be(5);
        snapshot.PublicState.Should().BeSameAs(publicState);
        snapshot.PrivateStatesByUserId.Should().ContainKey(userId);
    }

    [Fact]
    public async Task BroadcastGameStateAsync_CacheDisabled_DoesNotStoreSnapshot()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreatePublicState(gameId);
        var result = CreateBroadcastResult(publicState, "bob@example.com", CreatePrivateState(1), version: 1);
        var cache = CreateRealCache(enabled: false);

        var sut = CreateBroadcaster(
            buildResult: result, cache: cache,
            cacheEnabled: false, serveReconnects: false);

        await sut.BroadcastGameStateAsync(gameId);

        cache.TryGet(gameId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task BroadcastGameStateAsync_NullResult_EvictsCache()
    {
        var gameId = Guid.NewGuid();
        var cache = CreateRealCache(enabled: true);
        // Seed a snapshot so we can verify it gets evicted
        SeedCache(cache, gameId, version: 1);

        var sut = CreateBroadcaster(
            buildResult: null, cache: cache,
            cacheEnabled: true, serveReconnects: true);

        await sut.BroadcastGameStateAsync(gameId);

        cache.TryGet(gameId, out _).Should().BeFalse();
    }

    [Fact]
    public async Task BroadcastGameStateAsync_StaleVersion_RejectedByCache()
    {
        var gameId = Guid.NewGuid();
        var cache = CreateRealCache(enabled: true);
        // Seed a snapshot with version 10
        SeedCache(cache, gameId, version: 10);

        var publicState = CreatePublicState(gameId);
        var result = CreateBroadcastResult(publicState, "alice@example.com", CreatePrivateState(0), version: 5);

        var sut = CreateBroadcaster(
            buildResult: result, cache: cache,
            cacheEnabled: true, serveReconnects: true);

        await sut.BroadcastGameStateAsync(gameId);

        cache.TryGet(gameId, out var snapshot).Should().BeTrue();
        snapshot.VersionNumber.Should().Be(10, "stale write should be rejected");
    }

    // ── BroadcastGameStateToUserAsync cache hit ────────────────────────

    [Fact]
    public async Task BroadcastGameStateToUserAsync_CacheHitWithPrivateState_ServesFromCache()
    {
        var gameId = Guid.NewGuid();
        var userId = "alice@example.com";
        var publicState = CreatePublicState(gameId);
        var privateState = CreatePrivateState(0);
        var cache = CreateRealCache(enabled: true);
        SeedCache(cache, gameId, version: 1, userId, privateState, publicState);

        var userProxy = Substitute.For<IClientProxy>();
        userProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();

        var sut = CreateBroadcaster(
            tableStateBuilder: tableStateBuilder, cache: cache,
            cacheEnabled: true, serveReconnects: true,
            userProxy: userProxy);

        await sut.BroadcastGameStateToUserAsync(gameId, userId);

        // Should send both public and private from cache
        await userProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], publicState)),
            Arg.Any<CancellationToken>());

        await userProxy.Received(1).SendCoreAsync(
            "PrivateStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], privateState)),
            Arg.Any<CancellationToken>());

        // Should NOT have hit the DB
        await tableStateBuilder.DidNotReceive()
            .BuildPublicStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await tableStateBuilder.DidNotReceive()
            .BuildPrivateStateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── BroadcastGameStateToUserAsync cache miss ───────────────────────

    [Fact]
    public async Task BroadcastGameStateToUserAsync_CacheMiss_FallsBackToDb()
    {
        var gameId = Guid.NewGuid();
        var userId = "bob@example.com";
        var publicState = CreatePublicState(gameId);
        var privateState = CreatePrivateState(1);
        var cache = CreateRealCache(enabled: true); // empty cache

        var userProxy = Substitute.For<IClientProxy>();
        userProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(publicState);
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(privateState);

        var sut = CreateBroadcaster(
            tableStateBuilder: tableStateBuilder, cache: cache,
            cacheEnabled: true, serveReconnects: true,
            userProxy: userProxy);

        await sut.BroadcastGameStateToUserAsync(gameId, userId);

        // Should query DB
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
        await tableStateBuilder.Received(1)
            .BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>());

        // Should still send to user
        await userProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1),
            Arg.Any<CancellationToken>());
    }

    // ── BroadcastGameStateToUserAsync partial private miss ─────────────

    [Fact]
    public async Task BroadcastGameStateToUserAsync_PartialPrivateMiss_FallsBackForPrivateOnly()
    {
        var gameId = Guid.NewGuid();
        var cachedUserId = "alice@example.com";
        var missingUserId = "bob@example.com";
        var publicState = CreatePublicState(gameId);
        var alicePrivate = CreatePrivateState(0);
        var bobPrivate = CreatePrivateState(1);
        var cache = CreateRealCache(enabled: true);
        // Seed with alice's private state only
        SeedCache(cache, gameId, version: 3, cachedUserId, alicePrivate, publicState);

        var userProxy = Substitute.For<IClientProxy>();
        userProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPrivateStateAsync(gameId, missingUserId, Arg.Any<CancellationToken>())
            .Returns(bobPrivate);

        var sut = CreateBroadcaster(
            tableStateBuilder: tableStateBuilder, cache: cache,
            cacheEnabled: true, serveReconnects: true,
            userProxy: userProxy);

        await sut.BroadcastGameStateToUserAsync(gameId, missingUserId);

        // Should send cached public state
        await userProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], publicState)),
            Arg.Any<CancellationToken>());

        // Should fall back to DB for bob's private state only
        await tableStateBuilder.Received(1)
            .BuildPrivateStateAsync(gameId, missingUserId, Arg.Any<CancellationToken>());
        await tableStateBuilder.DidNotReceive()
            .BuildPublicStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // Should send the DB-rebuilt private state
        await userProxy.Received(1).SendCoreAsync(
            "PrivateStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], bobPrivate)),
            Arg.Any<CancellationToken>());

        // Should backfill the cache with bob's private state
        cache.TryGet(gameId, out var snapshot).Should().BeTrue();
        snapshot.PrivateStatesByUserId.Should().ContainKey(missingUserId);
    }

    [Fact]
    public async Task BroadcastGameStateToUserAsync_ServeReconnectsDisabled_AlwaysFallsBackToDb()
    {
        var gameId = Guid.NewGuid();
        var userId = "alice@example.com";
        var publicState = CreatePublicState(gameId);
        var privateState = CreatePrivateState(0);
        var cache = CreateRealCache(enabled: true);
        SeedCache(cache, gameId, version: 1, userId, privateState, publicState);

        var userProxy = Substitute.For<IClientProxy>();
        userProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(publicState);
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(privateState);

        var sut = CreateBroadcaster(
            tableStateBuilder: tableStateBuilder, cache: cache,
            cacheEnabled: true, serveReconnects: false,
            userProxy: userProxy);

        await sut.BroadcastGameStateToUserAsync(gameId, userId);

        // Should bypass cache and hit DB
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static GameStateBroadcaster CreateBroadcaster(
        BroadcastStateBuildResult? buildResult = null,
        ITableStateBuilder? tableStateBuilder = null,
        IActiveGameCache? cache = null,
        bool cacheEnabled = true,
        bool serveReconnects = true,
        IClientProxy? userProxy = null)
    {
        tableStateBuilder ??= Substitute.For<ITableStateBuilder>();
        if (buildResult is not null)
        {
            tableStateBuilder.BuildFullStateAsync(buildResult.PublicState.GameId, Arg.Any<CancellationToken>())
                .Returns(buildResult);
        }
        else
        {
            tableStateBuilder.BuildFullStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((BroadcastStateBuildResult?)null);
        }

        cache ??= Substitute.For<IActiveGameCache>();

        var actionTimerService = Substitute.For<IActionTimerService>();
        actionTimerService.GetTimerState(Arg.Any<Guid>()).Returns((ActionTimerState?)null);
        actionTimerService.IsTimerActive(Arg.Any<Guid>()).Returns(false);

        var clientProxy = userProxy ?? Substitute.For<IClientProxy>();
        clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(clientProxy);
        hubClients.User(Arg.Any<string>()).Returns(clientProxy);

        var hubContext = Substitute.For<IHubContext<GameHub>>();
        hubContext.Clients.Returns(hubClients);

        return new GameStateBroadcaster(
            hubContext,
            tableStateBuilder,
            actionTimerService,
            Substitute.For<IAutoActionService>(),
            cache,
            Substitute.For<IGameStateManager>(),
            Options.Create(new InMemoryEngineOptions()),
            TimeProvider.System,
            Options.Create(new ActiveGameCacheOptions
            {
                Enabled = cacheEnabled,
                ServeReconnectsFromCache = serveReconnects
            }),
            Substitute.For<ILogger<GameStateBroadcaster>>());
    }

    private static ActiveGameCache CreateRealCache(bool enabled)
    {
        var options = Options.Create(new ActiveGameCacheOptions { Enabled = enabled, ServeReconnectsFromCache = enabled });
        return new ActiveGameCache(options, Substitute.For<ILogger<ActiveGameCache>>(), new TestMeterFactory());
    }

    private static void SeedCache(
        ActiveGameCache cache,
        Guid gameId,
        ulong version,
        string? userId = null,
        PrivateStateDto? privateState = null,
        TableStatePublicDto? publicState = null)
    {
        var privates = ImmutableDictionary<string, PrivateStateDto>.Empty;
        var playerIds = ImmutableArray<string>.Empty;
        if (userId is not null && privateState is not null)
        {
            privates = privates.Add(userId, privateState);
            playerIds = [userId];
        }

        cache.Set(new CachedGameSnapshot
        {
            GameId = gameId,
            VersionNumber = version,
            PublicState = publicState ?? CreatePublicState(gameId),
            PrivateStatesByUserId = privates,
            PlayerUserIds = playerIds,
            HandNumber = 1,
            Phase = "Waiting",
            BuiltAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static BroadcastStateBuildResult CreateBroadcastResult(
        TableStatePublicDto publicState,
        string userId,
        PrivateStateDto privateState,
        ulong version)
    {
        return new BroadcastStateBuildResult(
            publicState,
            new Dictionary<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase)
            {
                [userId] = privateState
            },
            new[] { userId },
            VersionNumber: version,
            HandNumber: 1,
            Phase: publicState.CurrentPhase);
    }

    private static TableStatePublicDto CreatePublicState(Guid gameId) => new()
    {
        GameId = gameId,
        GameTypeCode = "FIVECARDDRAW",
        CurrentPhase = "Waiting",
        CurrentActorSeatIndex = -1,
        Seats = Array.Empty<SeatPublicDto>()
    };

    private static PrivateStateDto CreatePrivateState(int seatPosition) => new()
    {
        GameId = Guid.NewGuid(),
        PlayerName = $"Player{seatPosition}",
        SeatPosition = seatPosition,
        Hand = Array.Empty<CardPrivateDto>()
    };

    private sealed class TestMeterFactory : System.Diagnostics.Metrics.IMeterFactory
    {
        private readonly List<System.Diagnostics.Metrics.Meter> _meters = [];

        public System.Diagnostics.Metrics.Meter Create(System.Diagnostics.Metrics.MeterOptions options)
        {
            var meter = new System.Diagnostics.Metrics.Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var m in _meters) m.Dispose();
        }
    }
}
