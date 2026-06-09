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
		var source = CreateGameResponse(250, 1_000, true);

		var mapped = InvokeCreateLocalGameResponse(source);

		mapped.TournamentBuyIn.Should().Be(250);
		mapped.MaxBuyIn.Should().Be(1_000);
		mapped.RequiresJoinApproval.Should().BeTrue();
	}

	[Fact]
	public void InitializeJoinBuyInFromTable_UsesFixedTournamentBuyIn()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(250));

		InvokeInstanceMethod(tablePlay, "InitializeJoinBuyInFromTable");

		GetPrivateField<int>(tablePlay, "_joinBuyInAmount").Should().Be(250);
	}

	[Fact]
	public void IsLeagueTournament_ReturnsTrue_WhenTournamentBuyInIsSet()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(250));

		GetPrivateProperty<bool>(tablePlay, "IsLeagueTournament").Should().BeTrue();
	}

	[Fact]
	public void IsLeagueTournament_ReturnsFalse_WhenTournamentBuyInIsMissing()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(null));

		GetPrivateProperty<bool>(tablePlay, "IsLeagueTournament").Should().BeFalse();
	}

	[Fact]
	public void ShouldStartHoldEmFamilyGameViaGenericEndpoint_ReturnsTrue_ForHoldEmLeagueTournament()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(250));
		SetPrivateField(tablePlay, "_gameTypeCode", "HOLDEM");

		InvokeBooleanInstanceMethod(tablePlay, "ShouldStartHoldEmFamilyGameViaGenericEndpoint").Should().BeTrue();
	}

	[Fact]
	public void ShouldStartHoldEmFamilyGameViaGenericEndpoint_ReturnsFalse_ForNonTournamentHoldEmTable()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(null));
		SetPrivateField(tablePlay, "_gameTypeCode", "HOLDEM");

		InvokeBooleanInstanceMethod(tablePlay, "ShouldStartHoldEmFamilyGameViaGenericEndpoint").Should().BeFalse();
	}

	[Fact]
	public void ShouldStartHoldEmFamilyGameViaGenericEndpoint_ReturnsTrue_ForRedRiverLeagueTournament()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateGameResponse(250));
		SetPrivateField(tablePlay, "_gameTypeCode", "REDRIVER");

		InvokeBooleanInstanceMethod(tablePlay, "ShouldStartHoldEmFamilyGameViaGenericEndpoint").Should().BeTrue();
	}

	private static GetGameResponse InvokeCreateLocalGameResponse(GetGameResponse source)
	{
		var method =
			typeof(TablePlay).GetMethod("CreateLocalGameResponse", BindingFlags.Static | BindingFlags.NonPublic);
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

	private static bool InvokeBooleanInstanceMethod(TablePlay tablePlay, string methodName)
	{
		var method = typeof(TablePlay).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull($"TablePlay should expose {methodName} for Hold'Em-family start routing");

		var value = method!.Invoke(tablePlay, null);
		value.Should().BeOfType<bool>();
		return (bool)value!;
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

	private static GetGameResponse CreateGameResponse(int? tournamentBuyIn, int? maxBuyIn = null,
		bool requiresJoinApproval = false)
	{
		return new GetGameResponse(
			10,
			20,
			0,
			0,
			-1,
			true,
			DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"),
			"host-id",
			"host@example.com",
			-1,
			0,
			"WaitingForPlayers",
			"Waiting for players",
			-1,
			0,
			null,
			"{}",
			"FIVECARDDRAW",
			Guid.Parse("11111111-1111-1111-1111-111111111111"),
			"Five Card Draw",
			Guid.Parse("22222222-2222-2222-2222-222222222222"),
			8,
			20,
			2,
			"Tournament Table",
			123,
			"rv",
			10,
			0,
			null,
			GameStatus.WaitingForPlayers,
			DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"))
		{
			TournamentBuyIn = tournamentBuyIn,
			MaxBuyIn = maxBuyIn,
			RequiresJoinApproval = requiresJoinApproval
		};
	}
}