using System;
using System.Reflection;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayTournamentBuyInTests
{
    [Fact]
    public void CreateLocalGameResponse_PreservesTournamentBuyIn()
    {
        var source = CreateGameResponse(tournamentBuyIn: 250, maxBuyIn: 1_000, requiresJoinApproval: true);

        var mapped = InvokeCreateLocalGameResponse(source);

        mapped.TournamentBuyIn.Should().Be(250);
        mapped.MaxBuyIn.Should().Be(1_000);
        mapped.RequiresJoinApproval.Should().BeTrue();
    }

    [Fact]
    public void InitializeJoinBuyInFromTable_UsesFixedTournamentBuyIn()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(tournamentBuyIn: 250));

        InvokeInstanceMethod(tablePlay, "InitializeJoinBuyInFromTable");

        GetPrivateField<int>(tablePlay, "_joinBuyInAmount").Should().Be(250);
    }

    [Fact]
    public void IsLeagueTournament_ReturnsTrue_WhenTournamentBuyInIsSet()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(tournamentBuyIn: 250));

        GetPrivateProperty<bool>(tablePlay, "IsLeagueTournament").Should().BeTrue();
    }

    [Fact]
    public void IsLeagueTournament_ReturnsFalse_WhenTournamentBuyInIsMissing()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(tournamentBuyIn: null));

        GetPrivateProperty<bool>(tablePlay, "IsLeagueTournament").Should().BeFalse();
    }

    private static GetGameResponse InvokeCreateLocalGameResponse(GetGameResponse source)
    {
        var method = typeof(TablePlay).GetMethod("CreateLocalGameResponse", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should map the loaded game response through CreateLocalGameResponse");

        var result = method!.Invoke(null, [source]);
        result.Should().BeOfType<GetGameResponse>();
        return (GetGameResponse)result!;
    }

    private static void InvokeInstanceMethod(TablePlay tablePlay, string methodName)
    {
        var method = typeof(TablePlay).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"TablePlay should expose {methodName} for join buy-in initialization");
        method!.Invoke(tablePlay, null);
    }

    private static void SetPrivateField<T>(TablePlay tablePlay, string fieldName, T value)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        field!.SetValue(tablePlay, value);
    }

    private static T GetPrivateField<T>(TablePlay tablePlay, string fieldName)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");

        var value = field!.GetValue(tablePlay);
        value.Should().BeOfType<T>();
        return (T)value!;
    }

    private static T GetPrivateProperty<T>(TablePlay tablePlay, string propertyName)
    {
        var property = typeof(TablePlay).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"TablePlay should expose {propertyName} as a private property");

        var value = property!.GetValue(tablePlay);
        value.Should().BeOfType<T>();
        return (T)value!;
    }

    private static GetGameResponse CreateGameResponse(int? tournamentBuyIn, int? maxBuyIn = null, bool requiresJoinApproval = false)
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
            name: "Tournament Table",
            randomSeed: 123,
            rowVersion: "rv",
            smallBet: 10,
            smallBlind: 0,
            startedAt: null,
            status: GameStatus.WaitingForPlayers,
            updatedAt: DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"))
        {
            TournamentBuyIn = tournamentBuyIn,
            MaxBuyIn = maxBuyIn,
            RequiresJoinApproval = requiresJoinApproval
        };
    }
}