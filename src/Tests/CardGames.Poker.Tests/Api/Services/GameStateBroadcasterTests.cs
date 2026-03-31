#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.Cache;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameStateBroadcasterTests
{
    [Fact]
    public async Task BroadcastGameStateAsync_ScrewYourNeighborKeepOrTrade_UsesThirtySecondTimer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 3, currentPhaseRequiresAction: true);
        var (sut, tableStateBuilder, actionTimerService, _) = CreateSubject(publicState);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, 3, 30, Arg.Any<Func<Guid, int, Task>?>());
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastGameStateAsync_NonScrewYourNeighborAction_UsesDefaultTimer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "FIVECARDDRAW", "DrawPhase", 1);
        var (sut, _, actionTimerService, _) = CreateSubject(publicState);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, 1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>());
    }

    [Fact]
    public async Task BroadcastGameStateAsync_BobBarkerDrawPhase_UsesSharedTimer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "BOBBARKER", "DrawPhase", 1, currentPhaseRequiresAction: true);
        var (sut, _, actionTimerService, _) = CreateSubject(publicState);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, -1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>());
    }

    [Fact]
    public async Task BroadcastTableToastAsync_SendsToastToGameGroup()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 1);
        var (sut, _, _, clientProxy) = CreateSubject(publicState);

        await sut.BroadcastTableToastAsync(new TableToastNotificationDto
        {
            GameId = gameId,
            Message = "Starting new deck"
        });

        await clientProxy.Received(1).SendCoreAsync(
            "TableToastNotification",
            Arg.Is<object?[]>(args =>
                args.Length == 1 &&
                args[0] != null &&
                ((TableToastNotificationDto)args[0]!).GameId == gameId &&
                ((TableToastNotificationDto)args[0]!).Message == "Starting new deck"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastGameStateAsync_CachesStateAfterBroadcast()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "FIVECARDDRAW", "DrawPhase", 1);
        var gameCache = new ActiveGameCache(Substitute.For<ILogger<ActiveGameCache>>());
        var (sut, _, _, _) = CreateSubject(publicState, gameCache);

        await sut.BroadcastGameStateAsync(gameId);

        gameCache.TryGet(gameId, out var cached).Should().BeTrue();
        cached!.PublicState.Should().BeSameAs(publicState);
        cached.HandNumber.Should().Be(publicState.CurrentHandNumber);
    }

    [Fact]
    public async Task BroadcastGameStateAsync_CachesPrivateStatesPerPlayer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "FIVECARDDRAW", "DrawPhase", 1);
        var userId = "player@test.com";
        var privateState = new PrivateStateDto
        {
            GameId = gameId,
            PlayerName = "Test Player",
            SeatPosition = 0,
            Hand = Array.Empty<CardPrivateDto>()
        };

        var gameCache = new ActiveGameCache(Substitute.For<ILogger<ActiveGameCache>>());
        var (sut, tableStateBuilder, _, _) = CreateSubject(publicState, gameCache);
        tableStateBuilder.GetPlayerUserIdsAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new[] { userId });
        tableStateBuilder.BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>())
            .Returns(privateState);

        await sut.BroadcastGameStateAsync(gameId);

        gameCache.TryGet(gameId, out var cached).Should().BeTrue();
        cached!.PrivateStates.Should().ContainKey(userId);
        cached.PrivateStates[userId].Should().BeSameAs(privateState);
        cached.PlayerUserIds.Should().Contain(userId);
    }

    [Fact]
    public async Task BroadcastGameStateToUserAsync_ServesCachedStateOnReconnect()
    {
        var gameId = Guid.NewGuid();
        var userId = "player@test.com";
        var publicState = CreateState(gameId, "FIVECARDDRAW", "DrawPhase", 1);
        var privateState = new PrivateStateDto
        {
            GameId = gameId,
            PlayerName = "Test Player",
            SeatPosition = 0,
            Hand = Array.Empty<CardPrivateDto>()
        };

        var gameCache = new ActiveGameCache(Substitute.For<ILogger<ActiveGameCache>>());
        gameCache.Set(gameId, new CachedGameSnapshot
        {
            PublicState = publicState,
            PrivateStates = new Dictionary<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase) { [userId] = privateState },
            PlayerUserIds = new[] { userId },
            CapturedAt = DateTimeOffset.UtcNow,
            HandNumber = 1
        });

        var (sut, tableStateBuilder, _, _) = CreateSubject(publicState, gameCache);

        await sut.BroadcastGameStateToUserAsync(gameId, userId);

        // Should NOT hit the database (TableStateBuilder not called)
        await tableStateBuilder.DidNotReceive()
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
        await tableStateBuilder.DidNotReceive()
            .BuildPrivateStateAsync(gameId, userId, Arg.Any<CancellationToken>());
    }

    private static (GameStateBroadcaster Sut, ITableStateBuilder TableStateBuilder, IActionTimerService ActionTimerService, IClientProxy ClientProxy) CreateSubject(TableStatePublicDto publicState, IActiveGameCache? gameCache = null)
    {
        var tableStateBuilder = Substitute.For<ITableStateBuilder>();
        tableStateBuilder.BuildPublicStateAsync(publicState.GameId, Arg.Any<CancellationToken>())
            .Returns(publicState);
        tableStateBuilder.GetPlayerUserIdsAsync(publicState.GameId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        var actionTimerService = Substitute.For<IActionTimerService>();
        actionTimerService.GetTimerState(publicState.GameId).Returns((ActionTimerState?)null);
        actionTimerService.IsTimerActive(publicState.GameId).Returns(false);

        var clientProxy = Substitute.For<IClientProxy>();
        clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(clientProxy);
        hubClients.User(Arg.Any<string>()).Returns(clientProxy);

        var hubContext = Substitute.For<IHubContext<GameHub>>();
        hubContext.Clients.Returns(hubClients);

        var sut = new GameStateBroadcaster(
            hubContext,
            tableStateBuilder,
            actionTimerService,
            Substitute.For<IAutoActionService>(),
            gameCache ?? Substitute.For<IActiveGameCache>(),
            Substitute.For<ILogger<GameStateBroadcaster>>());

        return (sut, tableStateBuilder, actionTimerService, clientProxy);
    }



    private static TableStatePublicDto CreateState(
        Guid gameId,
        string gameTypeCode,
        string currentPhase,
        int currentActorSeatIndex,
        bool currentPhaseRequiresAction = false)
    {
        return new TableStatePublicDto
        {
            GameId = gameId,
            GameTypeCode = gameTypeCode,
            CurrentPhase = currentPhase,
            CurrentActorSeatIndex = currentActorSeatIndex,
            CurrentPhaseRequiresAction = currentPhaseRequiresAction,
            Seats = Array.Empty<SeatPublicDto>()
        };
    }
}