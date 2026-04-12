using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Profile.v1.Cashier;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;
using CardGames.Poker.Api.Infrastructure;

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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 0, StartingChips: 200));

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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 1, StartingChips: 100));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.InsufficientAccountChips);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(50);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 1, StartingChips: 200));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.BuyInExceedsTableMaximum);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(500);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 2, StartingChips: 200));

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

		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Tournament Mismatch", "tournament.mismatch@test.com");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 500,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetCurrentUser("tournament-mismatch-user-id", "Tournament Mismatch", "tournament.mismatch@test.com");

		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 3, StartingChips: 200));

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

		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 4, StartingChips: 250));

		result.IsT0.Should().BeTrue();
		var gamePlayer = await DbContext.GamePlayers.FirstAsync(x => x.GameId == game.Id && x.PlayerId == player.Id);
		gamePlayer.ChipStack.Should().Be(250);
	}

	[Fact]
	public async Task JoinGame_ScrewYourNeighborAfterStart_ReturnsLateJoinNotAllowedAndDoesNotSeatPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "SCREWYOURNEIGHBOR", ante: 25);
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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 0, StartingChips: 75));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.LateJoinNotAllowed);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_ScrewYourNeighborBeforeStart_AllowsPlayerToJoin()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "SCREWYOURNEIGHBOR", ante: 25);
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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 0, StartingChips: 10));

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
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 1, StartingChips: 200));

		// Assert
		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(JoinGameErrorCode.ZeroAccountBalance);

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(0);

		DbContext.GamePlayers.Count(x => x.GameId == game.Id && x.PlayerId == player.Id).Should().Be(0);
		DbContext.PlayerChipLedgerEntries.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BringIn).Should().Be(0);
	}

	[Fact]
	public async Task JoinGame_WithoutChipAccount_SeedsStartingBalanceAndSeatsPlayer()
	{
		// Arrange
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "No Wallet Yet", "no.wallet@test.com");

		SetCurrentUser("no-wallet-user-id", "No Wallet Yet", "no.wallet@test.com");

		// Act
		var result = await Mediator.Send(new JoinGameCommand(game.Id, SeatIndex: 2, StartingChips: 100));

		// Assert
		result.IsT0.Should().BeTrue();

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(CashierAccountInitializer.StartingChipAmount);

		var registrationLedgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.ReferenceType == CashierAccountInitializer.RegistrationReferenceType)
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
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", numberOfPlayers: 2, startingChips: 400);
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
