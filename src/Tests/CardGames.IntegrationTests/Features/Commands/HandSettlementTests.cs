using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Features.Commands;

public class HandSettlementTests : IntegrationTestBase
{
	private IPlayerChipWalletService WalletService =>
		Scope.ServiceProvider.GetRequiredService<IPlayerChipWalletService>();

	[Fact]
	public async Task HandSettlement_RecordsNetLossToLedger()
	{
		// Arrange
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Settlement Player", "settle@test.com");
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 1000,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		// Act
		await WalletService.RecordHandSettlementAsync(
			player.Id, netDelta: -150, game.Id, handNumber: 1, actorUserId: null, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		// Assert
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(850);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.SingleAsync();

		ledgerEntry.AmountDelta.Should().Be(-150);
		ledgerEntry.BalanceAfter.Should().Be(850);
		ledgerEntry.HandNumber.Should().Be(1);
		ledgerEntry.ReferenceId.Should().Be(game.Id);
	}

	[Fact]
	public async Task HandSettlement_WinCreditsAccount()
	{
		// Arrange
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Winner", "winner@test.com");
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 1000,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		// Act
		await WalletService.RecordHandSettlementAsync(
			player.Id, netDelta: 400, game.Id, handNumber: 1, actorUserId: null, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		// Assert
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(1400);

		var ledgerEntry = await DbContext.PlayerChipLedgerEntries
			.Where(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.SingleAsync();

		ledgerEntry.AmountDelta.Should().Be(400);
		ledgerEntry.BalanceAfter.Should().Be(1400);
		ledgerEntry.HandNumber.Should().Be(1);
	}

	[Fact]
	public async Task HandSettlement_ZeroDelta_NoOp()
	{
		// Arrange
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Break Even", "even@test.com");
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 1000,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		// Act
		await WalletService.RecordHandSettlementAsync(
			player.Id, netDelta: 0, game.Id, handNumber: 1, actorUserId: null, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		// Assert
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(1000);

		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id && x.Type == PlayerChipLedgerEntryType.HandSettlement)
			.Should().Be(0);
	}

	[Fact]
	public async Task HandSettlement_Idempotent_SkipsDuplicate()
	{
		// Arrange
		var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Dupe Player", "dupe@test.com");
		var game = await DatabaseSeeder.CreateGameAsync(DbContext, "FIVECARDDRAW");

		DbContext.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = 1000,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		// Act — call twice with the same (playerId, gameId, handNumber)
		await WalletService.RecordHandSettlementAsync(
			player.Id, netDelta: -200, game.Id, handNumber: 3, actorUserId: null, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		await WalletService.RecordHandSettlementAsync(
			player.Id, netDelta: -200, game.Id, handNumber: 3, actorUserId: null, CancellationToken.None);
		await DbContext.SaveChangesAsync();

		// Assert — only one ledger entry, balance debited once
		var account = await DbContext.PlayerChipAccounts.FirstAsync(x => x.PlayerId == player.Id);
		account.Balance.Should().Be(800);

		DbContext.PlayerChipLedgerEntries
			.Count(x => x.PlayerId == player.Id &&
			            x.Type == PlayerChipLedgerEntryType.HandSettlement &&
			            x.HandNumber == 3)
			.Should().Be(1);
	}
}
