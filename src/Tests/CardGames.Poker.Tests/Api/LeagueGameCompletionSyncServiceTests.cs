using System;
using System.Collections.Generic;
using System.Reflection;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api;

public class LeagueGameCompletionSyncServiceTests
{
	[Fact]
	public void DerivePlayerPlacementsFromGameState_OrdersWinnerBeforeEarlierEliminations()
	{
		var firstOutPlayer = new Player { Id = Guid.NewGuid(), Name = "First Out" };
		var secondOutPlayer = new Player { Id = Guid.NewGuid(), Name = "Second Out" };
		var winnerPlayer = new Player { Id = Guid.NewGuid(), Name = "Winner" };

		var game = new Game
		{
			CurrentPhase = "Ended",
			GamePlayers =
			[
				new GamePlayer
				{
					Id = Guid.NewGuid(),
					Player = firstOutPlayer,
					PlayerId = firstOutPlayer.Id,
					SeatPosition = 0,
					Status = GamePlayerStatus.Eliminated,
					ChipStack = 0,
					FinalChipCount = 0,
					JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
					LeftAt = DateTimeOffset.UtcNow.AddMinutes(-20)
				},
				new GamePlayer
				{
					Id = Guid.NewGuid(),
					Player = secondOutPlayer,
					PlayerId = secondOutPlayer.Id,
					SeatPosition = 1,
					Status = GamePlayerStatus.Eliminated,
					ChipStack = 0,
					FinalChipCount = 0,
					JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
					LeftAt = DateTimeOffset.UtcNow.AddMinutes(-10)
				},
				new GamePlayer
				{
					Id = Guid.NewGuid(),
					Player = winnerPlayer,
					PlayerId = winnerPlayer.Id,
					SeatPosition = 2,
					Status = GamePlayerStatus.Active,
					ChipStack = 150,
					JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
				}
			]
		};

		var placements = InvokeDerivePlayerPlacementsFromGameState(game);

		placements.Should().ContainInOrder(
			(winnerPlayer.Id.ToString(), 1),
			(secondOutPlayer.Id.ToString(), 2),
			(firstOutPlayer.Id.ToString(), 3));
	}

	private static List<(string PlayerNameOrUserId, int Placement)> InvokeDerivePlayerPlacementsFromGameState(Game game)
	{
		var method = typeof(LeagueGameCompletionSyncService)
			.GetMethod("DerivePlayerPlacementsFromGameState", BindingFlags.Static | BindingFlags.NonPublic);

		method.Should().NotBeNull();

		var result = method!.Invoke(null, [game]);
		result.Should().BeOfType<List<(string PlayerNameOrUserId, int Placement)>>();
		return (List<(string PlayerNameOrUserId, int Placement)>)result!;
	}
}