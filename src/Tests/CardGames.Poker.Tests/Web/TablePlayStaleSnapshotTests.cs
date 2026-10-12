using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static CardGames.Poker.Web.Components.Pages.TablePlay;

namespace CardGames.Poker.Tests.Web;

public class TablePlayStaleSnapshotTests
{
	[Fact]
	public async Task HandleTableStateUpdatedAsync_OlderHand_LeavesAppliedSnapshotUnchanged()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);

		await InvokeHandleTableStateUpdatedAsync(tablePlay, PublicState(gameId, 10, 10, pot: 5, actorSeatIndex: 0));

		GetPrivateField<TableStatePublicDto>(tablePlay, "_tableState").CurrentHandNumber.Should().Be(11);
		GetPrivateField<ulong>(tablePlay, "_appliedStateVersion").Should().Be(20);
		GetPrivateField<int>(tablePlay, "_pot").Should().Be(40);
		GetPrivateField<int>(tablePlay, "_currentActorSeatIndex").Should().Be(2);
		GetPrivateReference(tablePlay, "_gameResponse").Should().BeNull();
	}

	[Fact]
	public async Task HandleTableStateUpdatedAsync_SameHandOlderVersion_LeavesAppliedSnapshotUnchanged()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);

		await InvokeHandleTableStateUpdatedAsync(tablePlay, PublicState(gameId, 11, 19, pot: 5, actorSeatIndex: 0));

		GetPrivateField<TableStatePublicDto>(tablePlay, "_tableState").CurrentHandNumber.Should().Be(11);
		GetPrivateField<TableStatePublicDto>(tablePlay, "_tableState").StateVersion.Should().Be(20);
		GetPrivateField<ulong>(tablePlay, "_appliedStateVersion").Should().Be(20);
		GetPrivateField<int>(tablePlay, "_pot").Should().Be(40);
		GetPrivateField<int>(tablePlay, "_currentActorSeatIndex").Should().Be(2);
	}

	[Fact]
	public void ShouldIgnoreStaleTableState_KeepsEqualNewerAndUnversionedSnapshots()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);

		InvokeShouldIgnoreStaleTableState(tablePlay, PublicState(gameId, 11, 20)).Should().BeFalse();
		InvokeShouldIgnoreStaleTableState(tablePlay, PublicState(gameId, 12, 21)).Should().BeFalse();
		InvokeShouldIgnoreStaleTableState(tablePlay, PublicState(gameId, 11, 0)).Should().BeFalse();
	}

	[Fact]
	public void ShouldIgnoreStaleTableState_HandNumberOutranksAHigherVersion()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);

		InvokeShouldIgnoreStaleTableState(tablePlay, PublicState(gameId, 10, 21)).Should().BeTrue();
	}

	[Fact]
	public void ShouldIgnoreStalePrivateState_DropsOlderVersionAndKeepsEqualOrUnversioned()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_appliedStateVersion", 20UL);

		InvokeShouldIgnoreStalePrivateState(tablePlay, PrivateState(19)).Should().BeTrue();
		InvokeShouldIgnoreStalePrivateState(tablePlay, PrivateState(20)).Should().BeFalse();
		InvokeShouldIgnoreStalePrivateState(tablePlay, PrivateState(0)).Should().BeFalse();
	}

	[Fact]
	public async Task HandlePlayerActionPerformedAsync_OlderHand_LeavesLastActionDescriptionUnchanged()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);
		SetLogger(tablePlay);
		SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
		{
			new() { SeatIndex = 0, LastActionDescription = "Checked" }
		});

		await InvokeHandlePlayerActionPerformedAsync(tablePlay, new PlayerActionPerformedDto
		{
			GameId = gameId,
			SeatIndex = 0,
			ActionDescription = "Folded",
			HandNumber = 10,
			StateVersion = 10
		});

		GetPrivateField<List<SeatInfo>>(tablePlay, "_seats")[0].LastActionDescription.Should().Be("Checked");
	}

	[Fact]
	public async Task HandleActionTimerUpdatedAsync_OlderHandOrVersion_LeavesTimerUnchanged()
	{
		var gameId = Guid.NewGuid();
		var tablePlay = AppliedTable(gameId, handNumber: 11, stateVersion: 20, pot: 40, actorSeatIndex: 2);
		var current = new ActionTimerStateDto { SecondsRemaining = 15, HandNumber = 11, StateVersion = 20 };
		SetPrivateField(tablePlay, "_actionTimerState", current);

		await InvokeHandleActionTimerUpdatedAsync(tablePlay, new ActionTimerStateDto
		{
			SecondsRemaining = 3,
			HandNumber = 10,
			StateVersion = 10
		});
		await InvokeHandleActionTimerUpdatedAsync(tablePlay, new ActionTimerStateDto
		{
			SecondsRemaining = 4,
			HandNumber = 11,
			StateVersion = 19
		});

		GetPrivateField<ActionTimerStateDto>(tablePlay, "_actionTimerState").Should().BeSameAs(current);
	}

	[Fact]
	public void GameStateVersion_FromRowVersion_ReadsBigEndianAndTreatsShortArraysAsZero()
	{
		var newer = GameStateVersion.FromRowVersion([0, 0, 0, 0, 0, 0, 0, 2]);
		var older = GameStateVersion.FromRowVersion([0, 0, 0, 0, 0, 0, 0, 1]);

		newer.Should().BeGreaterThan(older);
		GameStateVersion.FromRowVersion([0, 0, 0, 0, 0, 0, 1]).Should().Be(0);
	}

	private static TablePlay AppliedTable(Guid gameId, int handNumber, ulong stateVersion, int pot, int actorSeatIndex)
	{
		var tablePlay = new TablePlay { GameId = gameId };
		SetPrivateField(tablePlay, "_tableState", PublicState(gameId, handNumber, stateVersion));
		SetPrivateField(tablePlay, "_appliedStateVersion", stateVersion);
		SetPrivateField(tablePlay, "_pot", pot);
		SetPrivateField(tablePlay, "_currentActorSeatIndex", actorSeatIndex);
		return tablePlay;
	}

	private static TableStatePublicDto PublicState(Guid gameId, int handNumber, ulong stateVersion, int pot = 0,
		int actorSeatIndex = -1)
	{
		return new TableStatePublicDto
		{
			GameId = gameId,
			CurrentPhase = "FirstBettingRound",
			CurrentHandNumber = handNumber,
			StateVersion = stateVersion,
			TotalPot = pot,
			CurrentActorSeatIndex = actorSeatIndex,
			Seats = []
		};
	}

	private static PrivateStateDto PrivateState(ulong stateVersion)
	{
		return new PrivateStateDto
		{
			GameId = Guid.NewGuid(),
			PlayerName = "Alice",
			StateVersion = stateVersion,
			Hand = []
		};
	}

	private static async Task InvokeHandleTableStateUpdatedAsync(TablePlay tablePlay, TableStatePublicDto state)
	{
		var method = typeof(TablePlay).GetMethod("HandleTableStateUpdatedAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		var task = (Task)method!.Invoke(tablePlay, [state])!;
		await task;
	}

	private static bool InvokeShouldIgnoreStaleTableState(TablePlay tablePlay, TableStatePublicDto state)
	{
		var method = typeof(TablePlay).GetMethod("ShouldIgnoreStaleTableState",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		return (bool)method!.Invoke(tablePlay, [state])!;
	}

	private static bool InvokeShouldIgnoreStalePrivateState(TablePlay tablePlay, PrivateStateDto state)
	{
		var method = typeof(TablePlay).GetMethod("ShouldIgnoreStalePrivateState",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		return (bool)method!.Invoke(tablePlay, [state])!;
	}

	private static async Task InvokeHandlePlayerActionPerformedAsync(TablePlay tablePlay, PlayerActionPerformedDto notification)
	{
		var method = typeof(TablePlay).GetMethod("HandlePlayerActionPerformedAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		var task = (Task)method!.Invoke(tablePlay, [notification])!;
		await task;
	}

	private static async Task InvokeHandleActionTimerUpdatedAsync(TablePlay tablePlay, ActionTimerStateDto timerState)
	{
		var method = typeof(TablePlay).GetMethod("HandleActionTimerUpdatedAsync",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		var task = (Task)method!.Invoke(tablePlay, [timerState])!;
		await task;
	}

	private static void SetLogger(TablePlay tablePlay)
	{
		var property = typeof(TablePlay).GetProperty("Logger",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		property!.SetValue(tablePlay, NullLogger<TablePlay>.Instance);
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

	private static object? GetPrivateReference(TablePlay tablePlay, string fieldName)
	{
		var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
		return field!.GetValue(tablePlay);
	}
}
