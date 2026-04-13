#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameStateBroadcasterTests
{
    [Fact]
    public async Task BroadcastGameStateAsync_KingsAndLowsDropOrStay_DelaysTimerUntilDealAnimationCompletes()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(
            gameId,
            "KINGSANDLOWS",
            "DropOrStay",
            3,
            currentPhaseRequiresAction: true,
            seats:
            [
                CreateSeat(0, 5, "Alice"),
                CreateSeat(1, 5, "Bob"),
                CreateSeat(2, 5, "Cara"),
                CreateSeat(3, 5, "Drew")
            ]);
        var (sut, tableStateBuilder, actionTimerService, _) = CreateSubject(publicState);
        var expectedStartDelay = TimeSpan.FromMilliseconds(15850);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, -1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), expectedStartDelay);
        await tableStateBuilder.Received(1)
            .BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastGameStateAsync_ScrewYourNeighborKeepOrTrade_UsesThirtySecondTimer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 3, currentPhaseRequiresAction: true);
        var (sut, tableStateBuilder, actionTimerService, _) = CreateSubject(publicState);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, 3, 30, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
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
            .StartTimer(gameId, 1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
    }

    [Fact]
    public async Task BroadcastGameStateAsync_BobBarkerDrawPhase_UsesSharedTimer()
    {
        var gameId = Guid.NewGuid();
        var publicState = CreateState(gameId, "BOBBARKER", "DrawPhase", 1, currentPhaseRequiresAction: true);
        var (sut, _, actionTimerService, _) = CreateSubject(publicState);

        await sut.BroadcastGameStateAsync(gameId);

        actionTimerService.Received(1)
            .StartTimer(gameId, -1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
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

    private static (GameStateBroadcaster Sut, ITableStateBuilder TableStateBuilder, IActionTimerService ActionTimerService, IClientProxy ClientProxy) CreateSubject(TableStatePublicDto publicState)
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

        var hubContext = Substitute.For<IHubContext<GameHub>>();
        hubContext.Clients.Returns(hubClients);

        var sut = new GameStateBroadcaster(
            hubContext,
            tableStateBuilder,
            actionTimerService,
            Substitute.For<IAutoActionService>(),
            Substitute.For<ILogger<GameStateBroadcaster>>());

        return (sut, tableStateBuilder, actionTimerService, clientProxy);
    }

    private static TableStatePublicDto CreateState(
        Guid gameId,
        string gameTypeCode,
        string currentPhase,
        int currentActorSeatIndex,
        bool currentPhaseRequiresAction = false,
        SeatPublicDto[]? seats = null)
    {
        return new TableStatePublicDto
        {
            GameId = gameId,
            GameTypeCode = gameTypeCode,
            CurrentPhase = currentPhase,
            CurrentActorSeatIndex = currentActorSeatIndex,
            CurrentPhaseRequiresAction = currentPhaseRequiresAction,
            Seats = seats ?? []
        };
    }

    private static SeatPublicDto CreateSeat(int seatIndex, int cardCount, string playerName)
    {
        return new SeatPublicDto
        {
            SeatIndex = seatIndex,
            IsOccupied = true,
            IsSittingOut = false,
            IsFolded = false,
            PlayerName = playerName,
            Cards = Enumerable.Range(0, cardCount)
                .Select(index => new CardPublicDto
                {
                    IsFaceUp = true,
                    Rank = ((index % 9) + 2).ToString(),
                    Suit = "Hearts"
                })
                .ToArray()
        };
    }
}