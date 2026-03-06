using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using NSubstitute;
using Refit;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class GameApiRouterTests
{
    [Fact]
    public async Task ProcessBettingActionAsync_HoldEm_RoutesToHoldEmApi()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new ProcessBettingActionRequest(BettingActionType.Check, 0);

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();

        var holdEmResponse = CreateFailedBettingActionResponse();
        holdEmApi
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(holdEmResponse));

        var fiveCardResponse = CreateFailedBettingActionResponse();
        fiveCardDrawApi
            .FiveCardDrawProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(fiveCardResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi);

        // Act
        _ = await sut.ProcessBettingActionAsync("HOLDEM", gameId, request);

        // Assert
        await holdEmApi.Received(1)
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>());

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessBettingActionAsync(gameId, Arg.Any<ProcessBettingActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDrawAsync_HoldEm_ReturnsNotSupportedAndDoesNotFallback()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var discardIndices = new List<int> { 0, 2 };

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi);

        // Act
        var response = await sut.ProcessDrawAsync("HOLDEM", gameId, playerId, discardIndices);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("Draw phase not supported for Texas Hold'Em.");

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessDrawAsync(Arg.Any<Guid>(), Arg.Any<ProcessDrawRequest>(), Arg.Any<CancellationToken>());
    }

    private static IApiResponse<ProcessBettingActionSuccessful> CreateFailedBettingActionResponse()
    {
        var response = Substitute.For<IApiResponse<ProcessBettingActionSuccessful>>();
        response.IsSuccessStatusCode.Returns(false);
        response.StatusCode.Returns(HttpStatusCode.BadRequest);
        response.Error.Returns((ApiException)null);
        return response;
    }
}