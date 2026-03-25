using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts;
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
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();

        var holdEmResponse = CreateFailedBettingActionResponse();
        holdEmApi
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(holdEmResponse));

        var fiveCardResponse = CreateFailedBettingActionResponse();
        fiveCardDrawApi
            .FiveCardDrawProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(fiveCardResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        _ = await sut.ProcessBettingActionAsync("HOLDEM", gameId, request);

        // Assert
        await holdEmApi.Received(1)
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>());

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessBettingActionAsync(gameId, Arg.Any<ProcessBettingActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBettingActionAsync_Omaha_RoutesToHoldEmApi()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new ProcessBettingActionRequest(BettingActionType.Check, 0);

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();

        var holdEmResponse = CreateFailedBettingActionResponse();
        holdEmApi
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(holdEmResponse));

        var fiveCardResponse = CreateFailedBettingActionResponse();
        fiveCardDrawApi
            .FiveCardDrawProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(fiveCardResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        _ = await sut.ProcessBettingActionAsync("OMAHA", gameId, request);

        // Assert
        await holdEmApi.Received(1)
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>());

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessBettingActionAsync(gameId, Arg.Any<ProcessBettingActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBettingActionAsync_BobBarker_RoutesToHoldEmApi()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new ProcessBettingActionRequest(BettingActionType.Check, 0);

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();
        var holdEmResponse = CreateFailedBettingActionResponse();

        holdEmApi
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(holdEmResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        _ = await sut.ProcessBettingActionAsync("BOBBARKER", gameId, request);

        // Assert
        await holdEmApi.Received(1)
            .HoldEmProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>());

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessBettingActionAsync(gameId, Arg.Any<ProcessBettingActionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBettingActionAsync_PairPressure_RoutesToPairPressureApi()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new ProcessBettingActionRequest(BettingActionType.Check, 0);

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();
        var pairPressureResponse = CreateFailedBettingActionResponse();

        pairPressureApi
            .PairPressureProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<ProcessBettingActionSuccessful>>(pairPressureResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        _ = await sut.ProcessBettingActionAsync("PAIRPRESSURE", gameId, request);

        // Assert
        await pairPressureApi.Received(1)
            .PairPressureProcessBettingActionAsync(gameId, request, Arg.Any<CancellationToken>());

        await sevenCardStudApi.DidNotReceive()
            .SevenCardStudProcessBettingActionAsync(gameId, Arg.Any<ProcessBettingActionRequest>(), Arg.Any<CancellationToken>());
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
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        var response = await sut.ProcessDrawAsync("HOLDEM", gameId, playerId, 0, discardIndices);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("Draw phase not supported for Texas Hold'Em.");

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessDrawAsync(Arg.Any<Guid>(), Arg.Any<ProcessDrawRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDrawAsync_Omaha_ReturnsNotSupportedAndDoesNotFallback()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var discardIndices = new List<int> { 0, 2 };

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        var response = await sut.ProcessDrawAsync("OMAHA", gameId, playerId, 0, discardIndices);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("Draw phase not supported for Omaha.");

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessDrawAsync(Arg.Any<Guid>(), Arg.Any<ProcessDrawRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDrawAsync_BobBarker_RoutesToShowcaseEndpoint()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        const int playerSeatIndex = 3;
        var discardIndices = new List<int> { 1 };

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();
        var showcaseResponse = CreateSuccessfulUnitResponse();

        gamesApi
            .BobBarkerSelectShowcaseAsync(
                gameId,
                Arg.Is<BobBarkerSelectShowcaseRequest>(r => r.ShowcaseCardIndex == 1 && r.PlayerSeatIndex == playerSeatIndex),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse>(showcaseResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        var response = await sut.ProcessDrawAsync("BOBBARKER", gameId, playerId, playerSeatIndex, discardIndices);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Content.Should().NotBeNull();
        response.Content!.Original.PlayerSeatIndex.Should().Be(playerSeatIndex);

        await gamesApi.Received(1)
            .BobBarkerSelectShowcaseAsync(
                gameId,
                Arg.Is<BobBarkerSelectShowcaseRequest>(r => r.ShowcaseCardIndex == 1 && r.PlayerSeatIndex == playerSeatIndex),
                Arg.Any<CancellationToken>());

        await fiveCardDrawApi.DidNotReceive()
            .FiveCardDrawProcessDrawAsync(Arg.Any<Guid>(), Arg.Any<ProcessDrawRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDrawAsync_BobBarker_WithMultipleSelections_ReturnsBadRequestWithoutCallingApi()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var discardIndices = new List<int> { 0, 1 };

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        var response = await sut.ProcessDrawAsync("BOBBARKER", gameId, playerId, 2, discardIndices);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Error.Should().Be("Select exactly one showcase card.");

        await gamesApi.DidNotReceive()
            .BobBarkerSelectShowcaseAsync(Arg.Any<Guid>(), Arg.Any<BobBarkerSelectShowcaseRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KeepOrTradeAsync_ScrewYourNeighbor_IgnoresTypedSuccessBodyForUnitResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var request = new KeepOrTradeRequest("Trade", playerId);

        var fiveCardDrawApi = Substitute.For<IFiveCardDrawApi>();
        var twosJacksApi = Substitute.For<ITwosJacksManWithTheAxeApi>();
        var kingsAndLowsApi = Substitute.For<IKingsAndLowsApi>();
        var sevenCardStudApi = Substitute.For<ISevenCardStudApi>();
        var pairPressureApi = Substitute.For<IPairPressureApi>();
        var goodBadUglyApi = Substitute.For<IGoodBadUglyApi>();
        var baseballApi = Substitute.For<IBaseballApi>();
        var followTheQueenApi = Substitute.For<IFollowTheQueenApi>();
        var holdEmApi = Substitute.For<IHoldEmApi>();
        var gamesApi = Substitute.For<IGamesApi>();
        var screwYourNeighborApi = Substitute.For<IScrewYourNeighborApi>();
        var tollboothApi = Substitute.For<ITollboothApi>();
        var keepOrTradeResponse = CreateSuccessfulKeepOrTradeResponse(gameId, playerId);

        screwYourNeighborApi
            .ScrewYourNeighborKeepOrTradeAsync(gameId, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<KeepOrTradeSuccessful>>(keepOrTradeResponse));

        var sut = new GameApiRouter(
            fiveCardDrawApi,
            twosJacksApi,
            kingsAndLowsApi,
            sevenCardStudApi,
            pairPressureApi,
            goodBadUglyApi,
            baseballApi,
            followTheQueenApi,
            holdEmApi,
            gamesApi,
            screwYourNeighborApi,
            tollboothApi);

        // Act
        var response = await sut.KeepOrTradeAsync("SCREWYOURNEIGHBOR", gameId, request);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await screwYourNeighborApi.Received(1)
            .ScrewYourNeighborKeepOrTradeAsync(gameId, request, Arg.Any<CancellationToken>());
    }

    private static IApiResponse<ProcessBettingActionSuccessful> CreateFailedBettingActionResponse()
    {
        var response = Substitute.For<IApiResponse<ProcessBettingActionSuccessful>>();
        response.IsSuccessStatusCode.Returns(false);
        response.StatusCode.Returns(HttpStatusCode.BadRequest);
        response.Error.Returns((ApiException)null);
        return response;
    }

    private static IApiResponse<KeepOrTradeSuccessful> CreateSuccessfulKeepOrTradeResponse(Guid gameId, Guid playerId)
    {
        var response = Substitute.For<IApiResponse<KeepOrTradeSuccessful>>();
        response.IsSuccessStatusCode.Returns(true);
        response.StatusCode.Returns(HttpStatusCode.OK);
        response.Content.Returns(new KeepOrTradeSuccessful(
            gameId,
            playerId,
            "Trade",
            didTrade: true,
            wasBlocked: false,
            nextPhase: "ResolveKeepOrTrade",
            nextPlayerSeatIndex: 2));
        return response;
    }

    private static IApiResponse CreateSuccessfulUnitResponse()
    {
        var response = Substitute.For<IApiResponse>();
        response.IsSuccessStatusCode.Returns(true);
        response.StatusCode.Returns(HttpStatusCode.OK);
        return response;
    }
}