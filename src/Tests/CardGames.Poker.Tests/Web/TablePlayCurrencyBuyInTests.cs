using System;
using System.Reflection;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Pages;
using CardGames.Poker.Web.Utilities;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayCurrencyBuyInTests
{
	[Fact]
	public void TypedBuyIn_KeepsAnOffStepDollarAmount()
	{
		var tablePlay = CreateTablePlay();

		InvokeInstanceMethod(tablePlay, "OnJoinBuyInInputChanged", new ChangeEventArgs { Value = "50.13" });
		InvokeInstanceMethod(tablePlay, "ClampJoinBuyIn");

		FormatAmount(tablePlay).Should().Be("$50.13");
	}

	[Fact]
	public void SliderBuyIn_SnapsOntoTheStepGrid()
	{
		var tablePlay = CreateTablePlay();

		InvokeInstanceMethod(tablePlay, "OnJoinBuyInSliderInput", new ChangeEventArgs { Value = "5013" });

		FormatAmount(tablePlay).Should().Be("$50.00");
	}

	private static TablePlay CreateTablePlay()
	{
		var tablePlay = new TablePlay();
		SetPrivateField(tablePlay, "_gameResponse", CreateCurrencyTableWithNoBettingIncrement());
		SetPrivateField(tablePlay, "_joinBuyInBalance", 100_000);
		return tablePlay;
	}

	private static string FormatAmount(TablePlay tablePlay) =>
		ChipAmountFormat.Format(GetPrivateField<int>(tablePlay, "_joinBuyInAmount"), asCurrency: true);

	private static void InvokeInstanceMethod(TablePlay tablePlay, string methodName, params object?[]? args)
	{
		var method = typeof(TablePlay).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		method.Should().NotBeNull($"TablePlay should expose {methodName}");
		method!.Invoke(tablePlay, args);
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

	private static GetGameResponse CreateCurrencyTableWithNoBettingIncrement()
	{
		return new GetGameResponse(
			0,
			0,
			null,
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
			"DEALERSCHOICE",
			Guid.Parse("11111111-1111-1111-1111-111111111111"),
			"Dealer's Choice",
			Guid.Parse("22222222-2222-2222-2222-222222222222"),
			8,
			0,
			2,
			"Currency Table",
			123,
			"rv",
			0,
			null,
			null,
			GameStatus.WaitingForPlayers,
			DateTimeOffset.Parse("2026-04-06T00:00:00+00:00"))
		{
			DisplayChipsAsCurrency = true,
			IsDealersChoice = true
		};
	}
}
