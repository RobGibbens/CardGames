#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Claims;
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

namespace CardGames.Poker.Tests.Api.Hubs;

public class GameHubTests
{
    // ── JoinGame: cache hit with full private state ─────────────────────

    [Fact]
    public async Task JoinGame_CacheHitWithPrivateState_SendsCachedState()
    {
        var gameId = Guid.NewGuid();
        var userId = "alice@example.com";
        var publicState = CreatePublicState(gameId);
        var privateState = CreatePrivateState(0);
        var snapshot = CreateSnapshot(gameId, version: 1, userId, privateState, publicState);

        var cache = Substitute.For<IActiveGameCache>();
        cache.TryGet(gameId, out Arg.Any<CachedGameSnapshot>()!)
            .Returns(x => { x[1] = snapshot; return true; });

        var callerProxy = Substitute.For<ISingleClientProxy>();
        callerProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();

        var sut = CreateHub(
            tableStateBuilder: tableStateBuilder,
            cache: cache,
            callerProxy: callerProxy,
            userId: userId,
            serveReconnects: true);

        await sut.JoinGame(gameId);

        // Should send public state from cache
        await callerProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], publicState)),
            Arg.Any<CancellationToken>());

        // Should send private state from cache
        await callerProxy.Received(1).SendCoreAsync(
            "PrivateStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], privateState)),
            Arg.Any<CancellationToken>());

        // Should NOT query DB
        await tableStateBuilder.DidNotReceive()
            .BuildPublicStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await tableStateBuilder.DidNotReceive()
            .BuildPrivateStateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── JoinGame: cache miss → falls back to DB ─────────────────────────

    [Fact]
    public async Task JoinGame_CacheMiss_FallsBackToDb()
    {
        var gameId = Guid.NewGuid();
        var userId = "bob@example.com";
        var publicState = CreatePublicState(gameId);
        var privateState = CreatePrivateState(1);

        var cache = Substitute.For<IActiveGameCache>();
        cache.TryGet(gameId, out Arg.Any<CachedGameSnapshot>()!)
            .Returns(false);

        var callerProxy = Substitute.For<ISingleClientProxy>();
        callerProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(publicState);
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(privateState);

        var sut = CreateHub(
            tableStateBuilder: tableStateBuilder,
            cache: cache,
            callerProxy: callerProxy,
            userId: userId,
            serveReconnects: true);

        await sut.JoinGame(gameId);

        // Should query DB
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
        await tableStateBuilder.Received(1)
            .BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>());

        // Should send to caller
        await callerProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1),
            Arg.Any<CancellationToken>());
        await callerProxy.Received(1).SendCoreAsync(
            "PrivateStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1),
            Arg.Any<CancellationToken>());
    }

    // ── JoinGame: partial private miss → backfills cache ────────────────

    [Fact]
    public async Task JoinGame_PartialPrivateMiss_RebuildsMissingPrivateAndBackfillsCache()
    {
        var gameId = Guid.NewGuid();
        var userId = "carol@example.com";
        var publicState = CreatePublicState(gameId);
        var dbPrivateState = CreatePrivateState(2);

        // Snapshot has public state but NOT carol's private state
        var snapshot = CreateSnapshot(gameId, version: 5, otherUserId: "alice@example.com",
            otherPrivateState: CreatePrivateState(0), publicState: publicState);

        var cache = Substitute.For<IActiveGameCache>();
        cache.TryGet(gameId, out Arg.Any<CachedGameSnapshot>()!)
            .Returns(x => { x[1] = snapshot; return true; });

        var callerProxy = Substitute.For<ISingleClientProxy>();
        callerProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(dbPrivateState);

        var sut = CreateHub(
            tableStateBuilder: tableStateBuilder,
            cache: cache,
            callerProxy: callerProxy,
            userId: userId,
            serveReconnects: true);

        await sut.JoinGame(gameId);

        // Should send cached public state
        await callerProxy.Received(1).SendCoreAsync(
            "TableStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], publicState)),
            Arg.Any<CancellationToken>());

        // Should NOT query DB for public state
        await tableStateBuilder.DidNotReceive()
            .BuildPublicStateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // Should query DB for missing private state
        await tableStateBuilder.Received(1)
            .BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>());

        // Should send the DB-built private state
        await callerProxy.Received(1).SendCoreAsync(
            "PrivateStateUpdated",
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], dbPrivateState)),
            Arg.Any<CancellationToken>());

        // Should backfill the cache
        cache.Received(1).UpsertPrivateState(gameId, userId, dbPrivateState, snapshot.VersionNumber);
    }

    // ── JoinGame: ServeReconnectsFromCache = false → always DB ──────────

    [Fact]
    public async Task JoinGame_ServeReconnectsDisabled_AlwaysFallsBackToDb()
    {
        var gameId = Guid.NewGuid();
        var userId = "dave@example.com";
        var publicState = CreatePublicState(gameId);

        var cache = Substitute.For<IActiveGameCache>();

        var callerProxy = Substitute.For<ISingleClientProxy>();
        callerProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(publicState);
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(CreatePrivateState(0));

        var sut = CreateHub(
            tableStateBuilder: tableStateBuilder,
            cache: cache,
            callerProxy: callerProxy,
            userId: userId,
            serveReconnects: false);

        await sut.JoinGame(gameId);

        // Should never check cache
        cache.DidNotReceive().TryGet(Arg.Any<Guid>(), out Arg.Any<CachedGameSnapshot>()!);

        // Should query DB
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
    }

    // ── JoinGame: no user identifier → throws HubException ──────────────

    [Fact]
    public async Task JoinGame_NoUserIdentifier_ThrowsHubException()
    {
        var gameId = Guid.NewGuid();

        var sut = CreateHub(
            tableStateBuilder: Substitute.For<ITableStateBuilder>(),
            cache: Substitute.For<IActiveGameCache>(),
            callerProxy: Substitute.For<ISingleClientProxy>(),
            userId: null,
            serveReconnects: true);

        var act = () => sut.JoinGame(gameId);

        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*identifier*");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static GameHub CreateHub(
        ITableStateBuilder tableStateBuilder,
        IActiveGameCache cache,
        ISingleClientProxy callerProxy,
        string? userId,
        bool serveReconnects)
    {
        var hubCallerClients = Substitute.For<IHubCallerClients>();
        hubCallerClients.Caller.Returns(callerProxy);

        var groupManager = Substitute.For<IGroupManager>();
        groupManager.AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Build a ClaimsPrincipal with email claim for the resolver
        ClaimsPrincipal? principal = null;
        if (userId is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, userId)], "Test");
            principal = new ClaimsPrincipal(identity);
        }

        var hubContext = Substitute.For<HubCallerContext>();
        hubContext.ConnectionId.Returns("test-connection-id");
        hubContext.UserIdentifier.Returns(userId);
        hubContext.User.Returns(principal);

        var userIdResolver = new GameUserIdResolver();

        var hub = new GameHub(
            tableStateBuilder,
            cache,
            Substitute.For<IGameStateManager>(),
            userIdResolver,
            Options.Create(new InMemoryEngineOptions()),
            Options.Create(new ActiveGameCacheOptions
            {
                Enabled = true,
                ServeReconnectsFromCache = serveReconnects
            }),
            Substitute.For<ILogger<GameHub>>())
        {
            Context = hubContext,
            Clients = hubCallerClients,
            Groups = groupManager
        };

        return hub;
    }

    private static CachedGameSnapshot CreateSnapshot(
        Guid gameId,
        ulong version,
        string? otherUserId = null,
        PrivateStateDto? otherPrivateState = null,
        TableStatePublicDto? publicState = null)
    {
        var privates = ImmutableDictionary<string, PrivateStateDto>.Empty;
        var playerIds = ImmutableArray<string>.Empty;
        if (otherUserId is not null && otherPrivateState is not null)
        {
            privates = privates.Add(otherUserId, otherPrivateState);
            playerIds = [otherUserId];
        }

        return new CachedGameSnapshot
        {
            GameId = gameId,
            VersionNumber = version,
            PublicState = publicState ?? CreatePublicState(gameId),
            PrivateStatesByUserId = privates,
            PlayerUserIds = playerIds,
            HandNumber = 1,
            Phase = "Waiting",
            BuiltAtUtc = DateTimeOffset.UtcNow
        };
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
}
