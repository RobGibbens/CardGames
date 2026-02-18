using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.IntegrationTests.Features.Commands;

public class JoinLeaveWalletCommandTests : IntegrationTestBase
{
	[Fact]
	public async Task JoinGame_WithSufficientAccountBalance_DebitsWalletAndCreatesBuyInLedger()
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

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(300);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id)
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync();

		ledgerEntry.Type.Should().Be(PlayerChipLedgerEntryType.BuyIn);
		ledgerEntry.AmountDelta.Should().Be(-200);
		ledgerEntry.BalanceAfter.Should().Be(300);
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
		DbContext.PlayerChipLedgerEntries.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.BuyIn).Should().Be(0);
	}

	[Fact]
	public async Task LeaveGame_BetweenHands_CreditsWalletAndCreatesCashOutLedger()
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

		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(375);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.CashOut)
			.OrderByDescending(x => x.OccurredAtUtc)
			.FirstAsync();

		ledgerEntry.AmountDelta.Should().Be(275);
		ledgerEntry.BalanceAfter.Should().Be(375);
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
