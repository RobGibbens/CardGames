using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using OneOf;

namespace CardGames.IntegrationTests.Features.Commands;

public class JoinLeaveWalletCommandTests : IntegrationTestBase
{
	[Fact]
	public async Task JoinGame_WithSufficientAccountBalance_ValidatesAndCreatesBringInLedger()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Wallet User", "wallet.user@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("wallet-user-id", "Wallet User", "wallet.user@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 0, 200));

		// Assert
		result.IsT0.Should().BeTrue();

		// Exposure-limit model: balance is NOT debited on join
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(500);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id)
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync();

		// Audit-only BringIn entry with zero delta
		ledgerEntry.Type.Should().Be(PlayerChipLedgerEntryType.BringIn);
		ledgerEntry.AmountDelta.Should().Be(0);
		ledgerEntry.BalanceAfter.Should().Be(500);
		ledgerEntry.ReferenceId.Should().Be(game.Id);

		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(200);
	}

	[Fact]
	public async Task JoinGame_WithInsufficientAccountBalance_ReturnsInsufficientFundsAndDoesNotSeatPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Short Stack", "short.stack@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 50,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("short-stack-user-id", "Short Stack", "short.stack@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 1, 100));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.InsufficientAccountChips);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(50);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_WithBuyInAboveTableMaximum_ReturnsConflictAndDoesNotSeatPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		game.MaxBuyIn = 150;

		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Capped Player", "capped.player@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("capped-player-user-id", "Capped Player", "capped.player@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 1, 200));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.BuyInExceedsTableMaximum);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(500);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_WithBuyInAtTableMaximum_Succeeds()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		game.MaxBuyIn = 200;

		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Exact Cap Player", "exact.cap@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("exact-cap-user-id", "Exact Cap Player", "exact.cap@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 2, 200));

		// Assert
		result.IsT0.Should().BeTrue();

		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(200);
	}

	[Fact]
	public async Task JoinGame_WithFixedTournamentBuyInMismatch_ReturnsInvalidStartingChips()
	{
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		game.TournamentBuyIn = 250;

		var player =
			await DatabaseSeeder.CreatePlayerAsync(DbContext, "Tournament Mismatch", "tournament.mismatch@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("tournament-mismatch-user-id", "Tournament Mismatch", "tournament.mismatch@test.com");

		var result = await Mediator.Send(new JoinGameCommand(game.Id, 3, 200));

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.InvalidStartingChips);
		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_WithFixedTournamentBuyInMatch_Succeeds()
	{
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		game.TournamentBuyIn = 250;

		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Tournament Match", "tournament.match@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("tournament-match-user-id", "Tournament Match", "tournament.match@test.com");

		var result = await Mediator.Send(new JoinGameCommand(game.Id, 4, 250));

		result.IsT0.Should().BeTrue();
		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(250);
	}

	[Fact]
	public async Task JoinGame_ScrewYourNeighborAfterStart_ReturnsLateJoinNotAllowedAndDoesNotSeatPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "SCREWYOURNEIGHBOR", 25);
		game.Status = GameStatus.InProgress;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = "KeepOrTrade";
		game.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Late SYN Player", "late.syn@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("late-syn-user-id", "Late SYN Player", "late.syn@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 0, 75));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.LateJoinNotAllowed);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_ScrewYourNeighborBeforeStart_AllowsPlayerToJoin()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "SCREWYOURNEIGHBOR", 25);
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Prestart SYN Player", "prestart.syn@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("prestart-syn-user-id", "Prestart SYN Player", "prestart.syn@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 0, 10));

		// Assert
		result.IsT0.Should().BeTrue();
		result.AsT0.CanPlayCurrentHand.Should().BeTrue();

		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(75);
	}

	[Fact]
	public async Task JoinGame_WithZeroAccountBalance_ReturnsZeroBalanceErrorAndDoesNotSeatPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Broke Player", "broke.player@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 0,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("broke-player-user-id", "Broke Player", "broke.player@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 1, 200));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.ZeroAccountBalance);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(0);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_WithoutChipAccount_SeedsStartingBalanceAndSeatsPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "No Wallet Yet", "no.wallet@test.com");

		SetCurrentUser("no-wallet-user-id", "No Wallet Yet", "no.wallet@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, 2, 100));

		// Assert
		result.IsT0.Should().BeTrue();

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var registrationLedgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id &&
			            x.ReferenceType == CashierAccountInitializer.RegistrationReferenceType)
			.SingleAsync();

		registrationLedgerEntry.Type.Should().Be(PlayerChipLedgerEntryType.Add);
		registrationLedgerEntry.AmountDelta.Should().Be(CashierAccountInitializer.StartingChipAmount);
		registrationLedgerEntry.BalanceAfter.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var bringInLedgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn)
			.SingleAsync();

		bringInLedgerEntry.AmountDelta.Should().Be(0);
		bringInLedgerEntry.BalanceAfter.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(100);
	}

	[Fact]
	public async Task LeaveGame_BetweenHands_WritesAuditOnlyCashOutLedger()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2,
			400);
		var player = setup.Players[0];
		var gamePlayer = setup.GamePlayers[0];

		setup.Game.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
		setup.Game.Status = GameStatus.InProgress;
		setup.Game.CurrentPhase = nameof(Phases.Complete);
		gamePlayer.ChipStack = 275;

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 100,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("player-1-user-id", player.Name, player.Email);

		// Act
		var result = await Mediator.Send(new LeaveGameCommand(setup.Game.Id));

		// Assert
		result.IsT0.Should().BeTrue();
		result.AsT0.Immediate.Should().BeTrue();
		result.AsT0.FinalChipCount.Should().Be(275);

		// Exposure-limit model: balance is NOT credited on leave (results already settled per-hand)
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(100);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.CashOut)
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync();

		// Audit-only CashOut entry with zero delta
		ledgerEntry.AmountDelta.Should().Be(0);
		ledgerEntry.BalanceAfter.Should().Be(100);
		ledgerEntry.ReferenceId.Should().Be(setup.Game.Id);

		var updatedGamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.Id == gamePlayer.Id);
		updatedGamePlayer.Status.Should().Be(GamePlayerStatus.Left);
	}

	[Fact]
	public async Task LeaveGame_FoldedPlayerDuringPreFlop_QueuesLeaveWithoutCharging()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.PreFlop), dealCarolACard: true);

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);

		var result = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));

		await AssertFoldedLeaveWasQueued(result, seeded.CarolSeat);
	}

	[Fact]
	public async Task LeaveGame_FoldedPlayerDuringShowdown_QueuesLeaveWithoutCharging()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.Showdown), dealCarolACard: true);

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);

		var result = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));

		await AssertFoldedLeaveWasQueued(result, seeded.CarolSeat);
	}

	[Fact]
	public async Task LeaveGame_FoldedPlayerDuringSecondBettingRound_QueuesLeaveWithoutCharging()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.SecondBettingRound), dealCarolACard: true);

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);

		var result = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));

		await AssertFoldedLeaveWasQueued(result, seeded.CarolSeat);
	}

	[Fact]
	public async Task LeaveGame_FoldedPlayerDuringComplete_LeavesImmediatelyWithAuditCashOut()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.Complete), dealCarolACard: true);
		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = seeded.Carol.Id,
			Balance = 100,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);

		var result = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));

		result.IsT0.Should().BeTrue();
		result.AsT0.Immediate.Should().BeTrue();

		var updated = await DbContext.GamePlayers.FirstAsync(x => x.Id == seeded.CarolSeat.Id);
		updated.Status.Should().Be(GamePlayerStatus.Left);
		updated.ChipStack.Should().Be(0);
		updated.FinalChipCount.Should().Be(490);

		var cashOuts = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == seeded.Carol.Id && x.Type == PlayerChipLedgerEntryType.CashOut)
			.ToListAsync();
		cashOuts.Should().ContainSingle();
		cashOuts[0].AmountDelta.Should().Be(0);

		var settlements = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == seeded.Carol.Id && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.ToListAsync();
		settlements.Should().BeEmpty();

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == seeded.Carol.Id);
		account.Balance.Should().Be(100);
	}

	[Fact]
	public async Task LeaveGame_FoldedPlayerWithNoCardForCurrentHand_LeavesImmediately()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.PreFlop), dealCarolACard: false);

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);

		var result = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));

		result.IsT0.Should().BeTrue();
		result.AsT0.Immediate.Should().BeTrue();

		var updated = await DbContext.GamePlayers.FirstAsync(x => x.Id == seeded.CarolSeat.Id);
		updated.Status.Should().Be(GamePlayerStatus.Left);
	}

	[Fact]
	public async Task SettleHand_QueuedFoldedPlayer_ChargesContributionOnce()
	{
		var seeded = await SeedFoldedCarolHandAsync(nameof(Phases.PreFlop), dealCarolACard: true);

		SetCurrentUser("carol-user-id", seeded.Carol.Name, seeded.Carol.Email);
		var leave = await Mediator.Send(new LeaveGameCommand(seeded.Game.Id));
		leave.IsT0.Should().BeTrue();
		leave.AsT0.Immediate.Should().BeFalse();

		seeded.AliceSeat.Status = GamePlayerStatus.Active;
		seeded.AliceSeat.TotalContributedThisHand = 30;
		seeded.AliceSeat.IsAllIn = false;
		seeded.BobSeat.Status = GamePlayerStatus.Active;
		seeded.BobSeat.TotalContributedThisHand = 10;
		seeded.BobSeat.HasFolded = true;
		seeded.BobSeat.IsAllIn = false;
		seeded.Game.CurrentHandNumber = 1;

		var now = DateTimeOffset.UtcNow;
		foreach (var player in new[] { seeded.Alice, seeded.Bob, seeded.Carol })
		{
			DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
			{
				PlayerId = player.Id,
				Balance = 100,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			});
		}

		await DbContext.SaveChangesAsync();

		var game = await DbContext.Games
			.Include(g => g.GamePlayers)
			.ThenInclude(gp => gp.Player)
			.FirstAsync(g => g.Id == seeded.Game.Id);

		var wallet = new PlayerChipWalletService(DbContext);
		var settlement = new HandSettlementService(wallet);
		var payouts = new Dictionary<string, int> { ["Alice"] = 50 };

		await settlement.SettleHandAsync(game, payouts, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		await AssertHandSettlement(seeded.Alice.Id, 20, 120);
		await AssertHandSettlement(seeded.Bob.Id, -10, 90);
		await AssertHandSettlement(seeded.Carol.Id, -10, 90);

		var carolSeat = await DbContext.GamePlayers.FirstAsync(x => x.Id == seeded.CarolSeat.Id);
		carolSeat.ChipStack.Should().Be(490);
		carolSeat.Status.Should().Be(GamePlayerStatus.Active);

		await settlement.SettleHandAsync(game, payouts, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		var carolSettlements = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == seeded.Carol.Id && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.ToListAsync();
		carolSettlements.Should().ContainSingle();

		var carolAccount = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == seeded.Carol.Id);
		carolAccount.Balance.Should().Be(90);
	}

	private async Task<FoldedCarolHand> SeedFoldedCarolHandAsync(string phase, bool dealCarolACard)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDEM", 3, 500);
		var alice = setup.Players[0];
		var bob = setup.Players[1];
		var carol = setup.Players[2];
		alice.Name = "Alice";
		bob.Name = "Bob";
		carol.Name = "Carol";

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.StartedAt = DateTimeOffset.UtcNow;
		game.Status = GameStatus.InProgress;
		game.CurrentPhase = phase;

		var carolSeat = setup.GamePlayers[2];
		carolSeat.HasFolded = true;
		carolSeat.ChipStack = 490;
		carolSeat.TotalContributedThisHand = 10;
		carolSeat.Status = GamePlayerStatus.Active;
		carolSeat.IsAllIn = false;
		carolSeat.IsSittingOut = false;
		carolSeat.LeftAtHandNumber = -1;

		if (dealCarolACard)
		{
			DbContext.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				GamePlayerId = carolSeat.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Ace,
				DealOrder = 1,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = DateTimeOffset.UtcNow,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}

		await DbContext.SaveChangesAsync();

		return new FoldedCarolHand(
			game,
			alice,
			bob,
			carol,
			setup.GamePlayers[0],
			setup.GamePlayers[1],
			carolSeat);
	}

	private async Task AssertFoldedLeaveWasQueued(
		OneOf<LeaveGameSuccessful, LeaveGameError> result,
		GamePlayer carolSeat)
	{
		result.IsT0.Should().BeTrue();
		result.AsT0.Immediate.Should().BeFalse();
		result.AsT0.LeftAtHandNumber.Should().Be(-1);
		result.AsT0.FinalChipCount.Should().BeNull();
		result.AsT0.Message.Should().Be("You will leave the table after the current hand completes.");

		var updated = await DbContext.GamePlayers.FirstAsync(x => x.Id == carolSeat.Id);
		updated.Status.Should().Be(GamePlayerStatus.Active);
		updated.ChipStack.Should().Be(490);
		updated.TotalContributedThisHand.Should().Be(10);
		updated.HasFolded.Should().BeTrue();
		updated.IsSittingOut.Should().BeTrue();
		updated.LeftAtHandNumber.Should().Be(1);
		updated.LeftAt.Should().BeNull();

		var ledgerRows = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == carolSeat.PlayerId &&
			            (x.Type == PlayerChipLedgerEntryType.HandSettlement ||
			             x.Type == PlayerChipLedgerEntryType.CashOut))
			.ToListAsync();
		ledgerRows.Should().BeEmpty();
	}

	private async Task AssertHandSettlement(Guid playerId, int amountDelta, int balanceAfter)
	{
		var rows = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == playerId && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.ToListAsync();
		rows.Should().ContainSingle();
		rows[0].AmountDelta.Should().Be(amountDelta);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == playerId);
		account.Balance.Should().Be(balanceAfter);
	}

	private sealed record FoldedCarolHand(
		Game Game,
		Player Alice,
		Player Bob,
		Player Carol,
		GamePlayer AliceSeat,
		GamePlayer BobSeat,
		GamePlayer CarolSeat);

	private void SetCurrentUser(string userId, string userName, string? userEmail)
	{
		var currentUser = Scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
		currentUser.Should().BeOfType<FakeCurrentUserService>();

		var fakeCurrentUser = (FakeCurrentUserService)currentUser;
		fakeCurrentUser.UserId = userId;
		fakeCurrentUser.UserName = userName;
		fakeCurrentUser.UserEmail = userEmail;
		fakeCurrentUser.IsAuthenticated = true;
	}
}