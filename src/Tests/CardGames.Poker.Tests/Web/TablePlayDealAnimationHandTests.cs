using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static CardGames.Poker.Web.Components.Pages.TablePlay;

namespace CardGames.Poker.Tests.Web;

public class TablePlayDealAnimationHandTests
{
	[Fact]
	public void PrepareDealAnimationForIncomingState_NewerHand_AbandonsInFlightDeal()
	{
		var tablePlay = CreateTablePlay();
		var pending = PrivateState(10);
		ArrangeDealAnimation(tablePlay, handNumber: 10, generation: 4, pending);
		var timer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
		SetPrivateField(tablePlay, "_dealTimer", timer);

		InvokePrepareDealAnimationForIncomingState(tablePlay, Snapshot(11, "K", "Hearts"));

		GetPrivateField<bool>(tablePlay, "_dealAnimationInProgress").Should().BeFalse();
		GetPrivateReference(tablePlay, "_dealTimer").Should().BeNull();
		GetPrivateField<Dictionary<int, List<CardInfo>>>(tablePlay, "_dealTargetCards").Should().BeEmpty();
		GetPrivateReference(tablePlay, "_dealPendingPrivateState").Should().BeNull();
		GetPrivateField<int>(tablePlay, "_dealAnimationGeneration").Should().Be(5);
	}

	[Fact]
	public void PrepareDealAnimationForIncomingState_SameHand_KeepsAnimationAndRetargetsState()
	{
		var tablePlay = CreateTablePlay();
		ArrangeDealAnimation(tablePlay, handNumber: 10, generation: 4, PrivateState(10));
		var incoming = Snapshot(10, "A", "Spades");

		InvokePrepareDealAnimationForIncomingState(tablePlay, incoming);

		GetPrivateField<bool>(tablePlay, "_dealAnimationInProgress").Should().BeTrue();
		GetPrivateField<TableStatePublicDto>(tablePlay, "_dealTargetState").CurrentHandNumber.Should().Be(10);
		GetPrivateField<Dictionary<int, List<CardInfo>>>(tablePlay, "_dealTargetCards")[0]
			.Should().ContainSingle(card => card.Rank == "A" && card.Suit == "Spades");
		GetPrivateField<int>(tablePlay, "_dealAnimationGeneration").Should().Be(4);
	}

	[Fact]
	public void PrepareDealAnimationForIncomingState_OlderHand_LeavesDealTargetsUnchanged()
	{
		var tablePlay = CreateTablePlay();
		ArrangeDealAnimation(tablePlay, handNumber: 11, generation: 4, PrivateState(11), rank: "K", suit: "Hearts");
		var originalTarget = Snapshot(11, "K", "Hearts");
		SetPrivateField(tablePlay, "_dealTargetState", originalTarget);

		InvokePrepareDealAnimationForIncomingState(tablePlay, Snapshot(10, "A", "Spades"));

		GetPrivateField<TableStatePublicDto>(tablePlay, "_dealTargetState").Should().BeSameAs(originalTarget);
		GetPrivateField<Dictionary<int, List<CardInfo>>>(tablePlay, "_dealTargetCards")[0]
			.Should().ContainSingle(card => card.Rank == "K" && card.Suit == "Hearts");
		GetPrivateField<bool>(tablePlay, "_dealAnimationInProgress").Should().BeTrue();
	}

	[Fact]
	public void StartCardDealAnimation_AfterNewerHandAbandon_CachesThatHandsFaces()
	{
		var tablePlay = CreateTablePlay();
		ArrangeDealAnimation(tablePlay, handNumber: 10, generation: 4, PrivateState(10));
		InvokePrepareDealAnimationForIncomingState(tablePlay, Snapshot(11, "K", "Hearts"));
		var generationAfterAbandon = GetPrivateField<int>(tablePlay, "_dealAnimationGeneration");

		InvokeStartCardDealAnimation(tablePlay, Snapshot(11, "K", "Hearts"));

		try
		{
			GetPrivateField<int>(tablePlay, "_dealAnimationHandNumber").Should().Be(11);
			GetPrivateField<int>(tablePlay, "_dealAnimationGeneration").Should().BeGreaterThan(generationAfterAbandon);
			GetPrivateField<Dictionary<int, List<CardInfo>>>(tablePlay, "_dealTargetCards")[0]
				.Should().ContainSingle(card => card.Rank == "K" && card.Suit == "Hearts" && card.IsFaceUp);
		}
		finally
		{
			DisposeDealTimer(tablePlay);
		}
	}

	[Fact]
	public void ShouldIgnorePrivateState_IgnoresOlderHandAndKeepsCurrentOrUnnumbered()
	{
		var tablePlay = CreateTablePlay();
		SetPrivateField(tablePlay, "_tableState", Snapshot(11, "K", "Hearts"));

		InvokeShouldIgnorePrivateState(tablePlay, PrivateState(10)).Should().BeTrue();
		InvokeShouldIgnorePrivateState(tablePlay, PrivateState(11)).Should().BeFalse();
		InvokeShouldIgnorePrivateState(tablePlay, PrivateState(0)).Should().BeFalse();
	}

	[Fact]
	public void ShouldDeferPrivateStateToDealAnimation_AcceptsMatchingHandAndRejectsOlder()
	{
		var tablePlay = CreateTablePlay();
		SetPrivateField(tablePlay, "_dealAnimationInProgress", true);
		SetPrivateField(tablePlay, "_dealAnimationHandNumber", 11);

		InvokeShouldDeferPrivateStateToDealAnimation(tablePlay, PrivateState(11)).Should().BeTrue();
		InvokeShouldDeferPrivateStateToDealAnimation(tablePlay, PrivateState(10)).Should().BeFalse();
	}

	private static TablePlay CreateTablePlay()
	{
		var tablePlay = new TablePlay();
		SetLogger(tablePlay);
		return tablePlay;
	}

	private static void ArrangeDealAnimation(
		TablePlay tablePlay,
		int handNumber,
		int generation,
		PrivateStateDto pending,
		string rank = "A",
		string suit = "Spades")
	{
		SetPrivateField(tablePlay, "_dealAnimationInProgress", true);
		SetPrivateField(tablePlay, "_dealAnimationHandNumber", handNumber);
		SetPrivateField(tablePlay, "_dealAnimationGeneration", generation);
		SetPrivateField(tablePlay, "_dealPendingPrivateState", pending);
		GetPrivateField<Dictionary<int, List<CardInfo>>>(tablePlay, "_dealTargetCards")[0] =
		[
			new CardInfo
			{
				Rank = rank,
				Suit = suit,
				IsFaceUp = true,
				DealOrder = 0
			}
		];
	}

	private static TableStatePublicDto Snapshot(int handNumber, string rank, string suit)
	{
		return new TableStatePublicDto
		{
			GameId = Guid.NewGuid(),
			CurrentPhase = "Dealing",
			CurrentHandNumber = handNumber,
			GameTypeCode = "SEVENCARDSTUD",
			Seats =
			[
				new SeatPublicDto
				{
					SeatIndex = 0,
					IsOccupied = true,
					Cards =
					[
						new CardPublicDto
						{
							IsFaceUp = true,
							Rank = rank,
							Suit = suit,
							DealOrder = 0
						}
					]
				}
			]
		};
	}

	private static PrivateStateDto PrivateState(int handNumber)
	{
		return new PrivateStateDto
		{
			GameId = Guid.NewGuid(),
			PlayerName = "alice",
			HandNumber = handNumber,
			Hand =
			[
				new CardPrivateDto
				{
					Rank = handNumber == 11 ? "K" : "A",
					Suit = handNumber == 11 ? "Hearts" : "Spades"
				}
			]
		};
	}

	private static void InvokePrepareDealAnimationForIncomingState(TablePlay tablePlay, TableStatePublicDto state)
	{
		var method = typeof(TablePlay).GetMethod("PrepareDealAnimationForIncomingState",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		method!.Invoke(tablePlay, [state]);
	}

	private static void InvokeStartCardDealAnimation(TablePlay tablePlay, TableStatePublicDto state)
	{
		var method = typeof(TablePlay).GetMethod("StartCardDealAnimation",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		method!.Invoke(tablePlay, [state, 0, 1, -1]);
	}

	private static bool InvokeShouldIgnorePrivateState(TablePlay tablePlay, PrivateStateDto state)
	{
		var method = typeof(TablePlay).GetMethod("ShouldIgnorePrivateState",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		return (bool)method!.Invoke(tablePlay, [state])!;
	}

	private static bool InvokeShouldDeferPrivateStateToDealAnimation(TablePlay tablePlay, PrivateStateDto state)
	{
		var method = typeof(TablePlay).GetMethod("ShouldDeferPrivateStateToDealAnimation",
			BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull();
		return (bool)method!.Invoke(tablePlay, [state])!;
	}

	private static void DisposeDealTimer(TablePlay tablePlay)
	{
		(GetPrivateReference(tablePlay, "_dealTimer") as Timer)?.Dispose();
		SetPrivateField<Timer?>(tablePlay, "_dealTimer", null);
	}

	private static object? GetPrivateReference(TablePlay tablePlay, string fieldName)
	{
		var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
		return field!.GetValue(tablePlay);
	}

	private static void SetLogger(TablePlay tablePlay)
	{
		var property = typeof(TablePlay).GetProperty("Logger",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		property.Should().NotBeNull("TablePlay should expose the injected logger property");
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
		return value.Should().BeAssignableTo<T>().Subject;
	}
}
