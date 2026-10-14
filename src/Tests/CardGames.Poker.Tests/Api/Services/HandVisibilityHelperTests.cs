using System.Collections.Generic;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class HandVisibilityHelperTests
{
	[Theory]
	[InlineData(1, true, "Showdown", null, true)]
	[InlineData(2, true, "Showdown", null, false)]
	[InlineData(1, true, "Complete", "All others folded", true)]
	[InlineData(1, false, "WaitingToStart", null, false)]
	public void IsWinByFoldHand_ClassifiesUncontestedFoldWins(
		int playersInHand,
		bool someoneFolded,
		string phase,
		string? winReason,
		bool expected)
	{
		var game = new Game { CurrentPhase = phase };
		var players = new List<GamePlayer>();

		for (var i = 0; i < playersInHand; i++)
			players.Add(new GamePlayer { Status = GamePlayerStatus.Active, HasFolded = false });

		if (someoneFolded)
			players.Add(new GamePlayer { Status = GamePlayerStatus.Active, HasFolded = true });

		var pots = string.IsNullOrWhiteSpace(winReason)
			? new List<Pot>()
			: new List<Pot> { new() { IsAwarded = true, WinReason = winReason } };

		HandVisibilityHelper.IsWinByFoldHand(game, players, pots).Should().Be(expected);
	}

	[Theory]
	[InlineData(1, true, "Showdown", null, true, false)]
	[InlineData(1, true, "Complete", "Lost to deck's pair of Jacks - pot carries forward", true, false)]
	[InlineData(1, true, "Complete", "Beat the deck with a pair of Sevens", true, false)]
	[InlineData(1, true, "Complete", "All others dropped", false, true)]
	[InlineData(1, true, "Showdown", null, false, true)]
	[InlineData(2, true, "Showdown", null, true, false)]
	public void IsWinByFoldHand_KingsAndLowsDeckHandIsNotAFoldWin(
		int playersInHand,
		bool someoneFolded,
		string phase,
		string? winReason,
		bool hasKingsAndLowsDeckHand,
		bool expected)
	{
		var game = new Game { CurrentPhase = phase };
		var players = new List<GamePlayer>();

		for (var i = 0; i < playersInHand; i++)
			players.Add(new GamePlayer { Status = GamePlayerStatus.Active, HasFolded = false });

		if (someoneFolded)
			players.Add(new GamePlayer { Status = GamePlayerStatus.Active, HasFolded = true });

		var pots = string.IsNullOrWhiteSpace(winReason)
			? new List<Pot>()
			: new List<Pot> { new() { IsAwarded = true, WinReason = winReason } };

		HandVisibilityHelper.IsWinByFoldHand(game, players, pots, hasKingsAndLowsDeckHand)
			.Should().Be(expected);
	}

	[Fact]
	public void IsUnawardedAllInRunout_ReturnsTrueWhileShowdownPotIsUnawarded()
	{
		var game = new Game
		{
			CurrentPhase = "Showdown",
			CurrentHandNumber = 1,
			GameSettings = """{"allInRunout":true,"runoutStreets":["SeventhStreet"],"runoutHandNumber":1}"""
		};

		HandVisibilityHelper.IsUnawardedAllInRunout(game, [new Pot { IsAwarded = false }])
			.Should().BeTrue();
	}

	[Fact]
	public void IsUnawardedAllInRunout_MissingRunoutHandNumberStillMatchesCurrentHand()
	{
		var game = new Game
		{
			CurrentPhase = "Showdown",
			CurrentHandNumber = 1,
			GameSettings = """{"allInRunout":true,"runoutStreets":["SeventhStreet"]}"""
		};

		HandVisibilityHelper.IsUnawardedAllInRunout(game, [new Pot { IsAwarded = false }])
			.Should().BeTrue();
	}

	[Theory]
	[InlineData("Complete", """{"allInRunout":true,"runoutHandNumber":1}""", 1, false)]
	[InlineData("FourthStreet", """{"allInRunout":true,"runoutHandNumber":1}""", 1, false)]
	[InlineData("Showdown", """{"allInRunout":false,"runoutHandNumber":1}""", 1, false)]
	[InlineData("Showdown", """{"allInRunout":"true","runoutHandNumber":1}""", 1, false)]
	[InlineData("Showdown", """{"allInRunout":true,"runoutHandNumber":2}""", 1, false)]
	[InlineData("Showdown", null, 1, false)]
	[InlineData("Showdown", "", 1, false)]
	[InlineData("Showdown", "{", 1, false)]
	public void IsUnawardedAllInRunout_ReturnsFalseOutsideTheWindow(
		string phase,
		string? gameSettings,
		int currentHandNumber,
		bool potAwarded)
	{
		var game = new Game
		{
			CurrentPhase = phase,
			CurrentHandNumber = currentHandNumber,
			GameSettings = gameSettings
		};

		HandVisibilityHelper.IsUnawardedAllInRunout(game, [new Pot { IsAwarded = potAwarded }])
			.Should().BeFalse();
	}

	[Fact]
	public void IsUnawardedAllInRunout_AwardedCurrentHandPotEndsTheWindow()
	{
		var game = new Game
		{
			CurrentPhase = "Showdown",
			CurrentHandNumber = 1,
			GameSettings = """{"allInRunout":true,"runoutStreets":["SeventhStreet"],"runoutHandNumber":1}"""
		};

		HandVisibilityHelper.IsUnawardedAllInRunout(game, [new Pot { IsAwarded = true }])
			.Should().BeFalse();
	}
}
