#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class HandSettlementServiceTests
{
	[Fact]
	public async Task SettleHandAsync_SettlesOnlyActiveOrAllInParticipantsWithNonZeroNet()
	{
		var gameId = Guid.NewGuid();
		var aliceId = Guid.NewGuid();
		var bobId = Guid.NewGuid();
		var drewId = Guid.NewGuid();
		var erinId = Guid.NewGuid();
		var walletService = Substitute.For<IPlayerChipWalletService>();
		var sut = new HandSettlementService(walletService);
		var game = new Game
		{
			Id = gameId,
			CurrentPhase = "Showdown",
			CurrentHandNumber = 7,
			GamePlayers =
			[
				CreateParticipant(aliceId, "Alice", GamePlayerStatus.Active, totalContributedThisHand: 10),
				CreateParticipant(bobId, "Bob", GamePlayerStatus.SittingOut, totalContributedThisHand: 40, isAllIn: true),
				CreateParticipant(drewId, "Drew", GamePlayerStatus.Active, totalContributedThisHand: 30),
				CreateParticipant(erinId, "Erin", GamePlayerStatus.Left, totalContributedThisHand: 20)
			]
		};
		var payouts = new Dictionary<string, int>
		{
			["Alice"] = 70,
			["Drew"] = 30,
			["Erin"] = 50
		};

		await sut.SettleHandAsync(game, payouts, CancellationToken.None);

		await walletService.Received(1).RecordHandSettlementAsync(
			aliceId,
			60,
			gameId,
			7,
			null,
			Arg.Any<CancellationToken>());
		await walletService.Received(1).RecordHandSettlementAsync(
			bobId,
			-40,
			gameId,
			7,
			null,
			Arg.Any<CancellationToken>());
		await walletService.DidNotReceive().RecordHandSettlementAsync(
			drewId,
			Arg.Any<int>(),
			gameId,
			7,
			null,
			Arg.Any<CancellationToken>());
		await walletService.DidNotReceive().RecordHandSettlementAsync(
			erinId,
			Arg.Any<int>(),
			gameId,
			7,
			null,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task SettleHandAsync_WhenEveryParticipantBreaksEven_DoesNotWriteSettlements()
	{
		var walletService = Substitute.For<IPlayerChipWalletService>();
		var sut = new HandSettlementService(walletService);
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = "Showdown",
			CurrentHandNumber = 4,
			GamePlayers =
			[
				CreateParticipant(Guid.NewGuid(), "Alice", GamePlayerStatus.Active, totalContributedThisHand: 25),
				CreateParticipant(Guid.NewGuid(), "Bob", GamePlayerStatus.Active, totalContributedThisHand: 15, isAllIn: true)
			]
		};
		var payouts = new Dictionary<string, int>
		{
			["Alice"] = 25,
			["Bob"] = 15
		};

		await sut.SettleHandAsync(game, payouts, CancellationToken.None);

		await walletService.DidNotReceiveWithAnyArgs().RecordHandSettlementAsync(
			default,
			default,
			default,
			default,
			default,
			default);
	}

	private static GamePlayer CreateParticipant(
		Guid playerId,
		string playerName,
		GamePlayerStatus status,
		int totalContributedThisHand,
		bool isAllIn = false)
	{
		return new GamePlayer
		{
			PlayerId = playerId,
			Player = new Player
			{
				Id = playerId,
				Name = playerName,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			},
			Status = status,
			IsAllIn = isAllIn,
			TotalContributedThisHand = totalContributedThisHand,
			JoinedAt = DateTimeOffset.UtcNow
		};
	}
}