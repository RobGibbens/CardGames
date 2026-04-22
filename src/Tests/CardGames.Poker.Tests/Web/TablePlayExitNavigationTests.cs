using System;
using System.Reflection;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayExitNavigationTests
{
    [Fact]
    public void GetExitDestination_ReturnsLeagueDetailRoute_WhenLeagueIdIsPresent()
    {
        var leagueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var result = InvokeGetExitDestination(leagueId);

        result.Should().Be($"/leagues/{leagueId}");
    }

    [Fact]
    public void GetExitDestination_ReturnsLobby_WhenLeagueIdIsMissing()
    {
        var result = InvokeGetExitDestination(null);

        result.Should().Be("/lobby");
    }

    [Fact]
    public void CreateLocalGameResponse_PreservesLeagueId()
    {
        var leagueId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var source = CreateGameResponse(leagueId);

        var result = InvokeCreateLocalGameResponse(source);

        result.LeagueId.Should().Be(leagueId);
    }

    private static string InvokeGetExitDestination(Guid? leagueId)
    {
        var method = typeof(TablePlay).GetMethod("GetExitDestination", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should expose GetExitDestination for exit-route selection");

        var result = method!.Invoke(null, [leagueId]);
        result.Should().BeOfType<string>();
        return (string)result!;
    }

    private static GetGameResponse InvokeCreateLocalGameResponse(GetGameResponse source)
    {
        var method = typeof(TablePlay).GetMethod("CreateLocalGameResponse", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should map loaded game responses through CreateLocalGameResponse");

        var result = method!.Invoke(null, [source]);
        result.Should().BeOfType<GetGameResponse>();
        return (GetGameResponse)result!;
    }

    private static GetGameResponse CreateGameResponse(Guid? leagueId)
    {
        return new GetGameResponse(
            ante: 10,
            bigBet: 20,
            bigBlind: 0,
            bringIn: 0,
            bringInPlayerIndex: -1,
            canContinue: true,
            createdAt: DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"),
            createdById: "host-id",
            createdByName: "host@example.com",
            currentDrawPlayerIndex: -1,
            currentHandNumber: 0,
            currentPhase: "WaitingForPlayers",
            currentPhaseDescription: "Waiting for players",
            currentPlayerIndex: -1,
            dealerPosition: 0,
            endedAt: null,
            gameSettings: "{}",
            gameTypeCode: "FIVECARDDRAW",
            gameTypeId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            gameTypeName: "Five Card Draw",
            id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            maximumNumberOfPlayers: 8,
            minBet: 20,
            minimumNumberOfPlayers: 2,
            name: "League Table",
            randomSeed: 123,
            rowVersion: "rv",
            smallBet: 10,
            smallBlind: 0,
            startedAt: null,
            status: GameStatus.WaitingForPlayers,
            updatedAt: DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"))
        {
            LeagueId = leagueId
        };
    }
}