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
}
