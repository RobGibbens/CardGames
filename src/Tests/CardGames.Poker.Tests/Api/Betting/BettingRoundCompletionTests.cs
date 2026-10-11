using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Betting;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Betting;

public class BettingRoundCompletionTests
{
	[Fact]
	public void LimpedPot_SmallBlindJustCalled_BigBlindHasNotActed_RoundStaysOpen()
	{
		var (players, round) = LimpedThreeHanded();
		var acted = new HashSet<Guid> { players[0].Id, players[1].Id };

		BettingRoundCompletion.IsRoundComplete(players, round, nextPlayerIndex: 2, acted)
			.Should().BeFalse();
	}

	[Fact]
	public void LimpedPot_AllThreePlayersActed_RoundCloses()
	{
		var (players, round) = LimpedThreeHanded();
		var acted = players.Select(player => player.Id).ToHashSet();

		BettingRoundCompletion.IsRoundComplete(players, round, nextPlayerIndex: 0, acted)
			.Should().BeTrue();
	}

	[Fact]
	public void FoldBeforeBigBlind_MatchedCalls_DoesNotCloseUntilBigBlindActs()
	{
		var button = Player(0, currentBet: 10);
		var smallBlind = Player(1, currentBet: 10);
		var bigBlind = Player(2, currentBet: 10);
		var utg = Player(3, currentBet: 0, hasFolded: true);
		var round = Round(currentBet: 10, lastAggressorIndex: -1);

		var acted = new HashSet<Guid> { utg.Id, button.Id, smallBlind.Id };

		BettingRoundCompletion.IsRoundComplete(
				[button, smallBlind, bigBlind, utg],
				round,
				nextPlayerIndex: 2,
				acted)
			.Should().BeFalse();
	}

	[Fact]
	public void RaisedPot_ActionReturnsToLastAggressor_AllMatched_RoundCloses()
	{
		var raiser = Player(0, currentBet: 30);
		var smallBlind = Player(1, currentBet: 30);
		var bigBlind = Player(2, currentBet: 30);
		var round = Round(currentBet: 30, lastAggressorIndex: 0);
		var acted = new HashSet<Guid> { raiser.Id, smallBlind.Id, bigBlind.Id };

		BettingRoundCompletion.IsRoundComplete(
				[raiser, smallBlind, bigBlind],
				round,
				nextPlayerIndex: 0,
				acted)
			.Should().BeTrue();
	}

	[Fact]
	public void PlayerWhoAlreadyActed_StillOwesTheNewBet_RoundStaysOpen()
	{
		var checker = Player(0, currentBet: 0);
		var bettor = Player(1, currentBet: 20);
		var bigBlind = Player(2, currentBet: 10);
		var round = Round(currentBet: 20, lastAggressorIndex: 1);
		var acted = new HashSet<Guid> { checker.Id, bettor.Id };

		BettingRoundCompletion.IsRoundComplete(
				[checker, bettor, bigBlind],
				round,
				nextPlayerIndex: 2,
				acted)
			.Should().BeFalse();
	}

	[Fact]
	public void OnePlayerRemains_RoundCloses()
	{
		var winner = Player(0, currentBet: 10);
		var folded = Player(1, currentBet: 0, hasFolded: true);
		var round = Round(currentBet: 10, lastAggressorIndex: -1);

		BettingRoundCompletion.IsRoundComplete([winner, folded], round, nextPlayerIndex: -1, new HashSet<Guid>())
			.Should().BeTrue();
	}

	[Fact]
	public void ActedPlayerIds_IncludesCurrentPlayerWhenRecordIsNotOnTheRoundYet()
	{
		var earlier = Guid.NewGuid();
		var current = Guid.NewGuid();
		var round = Round(currentBet: 10, lastAggressorIndex: -1);
		round.Actions.Add(new BettingActionRecord { GamePlayerId = earlier });

		var acted = BettingRoundCompletion.ActedPlayerIds(round, current);

		acted.Should().BeEquivalentTo([earlier, current]);
	}

	private static (List<GamePlayer> Players, BettingRound Round) LimpedThreeHanded()
	{
		var players = new List<GamePlayer>
		{
			Player(0, currentBet: 10),
			Player(1, currentBet: 10),
			Player(2, currentBet: 10)
		};
		return (players, Round(currentBet: 10, lastAggressorIndex: -1));
	}

	private static GamePlayer Player(int seat, int currentBet, bool hasFolded = false, bool isAllIn = false)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			SeatPosition = seat,
			CurrentBet = currentBet,
			HasFolded = hasFolded,
			IsAllIn = isAllIn,
			ChipStack = isAllIn ? 0 : 100
		};
	}

	private static BettingRound Round(int currentBet, int lastAggressorIndex)
	{
		return new BettingRound
		{
			Street = "PreFlop",
			CurrentBet = currentBet,
			LastAggressorIndex = lastAggressorIndex
		};
	}
}
