#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class PlayerChipWalletServiceTests
{
	[Fact]
	public async Task GetBalanceAsync_WhenAccountIsMissing_CreatesAndPersistsRegistrationCredit()
	{
		var playerId = Guid.NewGuid();

		await using var context = CreateContext();
		var sut = new PlayerChipWalletService(context);

		var balance = await sut.GetBalanceAsync(playerId, CancellationToken.None);

		balance.Should().Be(100_000);

		var account = await context.PlayerChipAccounts.SingleAsync(entry => entry.PlayerId == playerId);
		account.Balance.Should().Be(100_000);

		var registrationEntries = await context.PlayerChipLedgerEntries
			.Where(entry =>
				entry.PlayerId == playerId &&
				entry.Type == PlayerChipLedgerEntryType.Add)
			.ToListAsync();

		registrationEntries.Should().ContainSingle();
		registrationEntries[0].AmountDelta.Should().Be(100_000);
		registrationEntries[0].BalanceAfter.Should().Be(100_000);
	}

	[Fact]
	public async Task TryDebitForBuyInAsync_WhenAmountIsNotPositive_ReturnsFailureWithoutCreatingEntries()
	{
		await using var context = CreateContext();
		var sut = new PlayerChipWalletService(context);

		var result = await sut.TryDebitForBuyInAsync(
			Guid.NewGuid(),
			0,
			Guid.NewGuid(),
			"actor-user",
			CancellationToken.None);

		result.Succeeded.Should().BeFalse();
		result.BalanceAfter.Should().BeNull();
		result.ErrorMessage.Should().Be("Buy-in amount must be greater than 0.");
		context.PlayerChipAccounts.Local.Should().BeEmpty();
		context.PlayerChipLedgerEntries.Local.Should().BeEmpty();
	}

	[Fact]
	public async Task TryDebitForBuyInAsync_WhenAmountMatchesBalance_WritesAuditOnlyBringInEntry()
	{
		const int startingBalance = 100_000;
		var playerId = Guid.NewGuid();
		var gameId = Guid.NewGuid();

		await using var context = CreateContext();
		var sut = new PlayerChipWalletService(context);

		var result = await sut.TryDebitForBuyInAsync(
			playerId,
			startingBalance,
			gameId,
			"actor-user",
			CancellationToken.None);

		await context.SaveChangesAsync();

		result.Succeeded.Should().BeTrue();
		result.BalanceAfter.Should().Be(startingBalance);

		var account = await context.PlayerChipAccounts.SingleAsync(entry => entry.PlayerId == playerId);
		account.Balance.Should().Be(startingBalance);

		var registrationEntry = await context.PlayerChipLedgerEntries.SingleAsync(entry =>
			entry.PlayerId == playerId &&
			entry.Type == PlayerChipLedgerEntryType.Add);
		registrationEntry.AmountDelta.Should().Be(startingBalance);

		var bringInEntry = await context.PlayerChipLedgerEntries.SingleAsync(entry =>
			entry.PlayerId == playerId &&
			entry.Type == PlayerChipLedgerEntryType.BringIn);
		bringInEntry.AmountDelta.Should().Be(0);
		bringInEntry.BalanceAfter.Should().Be(startingBalance);
		bringInEntry.ReferenceId.Should().Be(gameId);
		bringInEntry.Reason.Should().Contain("exposure limit");
		bringInEntry.Reason.Should().Contain("100,000");
	}

	[Fact]
	public async Task CreditForCashOutAsync_WhenAmountIsPositive_WritesAuditOnlyCashOutEntryWithoutChangingBalance()
	{
		var playerId = Guid.NewGuid();
		var gameId = Guid.NewGuid();

		await using var context = CreateContext();
		var sut = new PlayerChipWalletService(context);

		var balanceAfter = await sut.CreditForCashOutAsync(
			playerId,
			425,
			gameId,
			"actor-user",
			CancellationToken.None);

		await context.SaveChangesAsync();

		balanceAfter.Should().Be(100_000);

		var account = await context.PlayerChipAccounts.SingleAsync(entry => entry.PlayerId == playerId);
		account.Balance.Should().Be(100_000);

		var cashOutEntry = await context.PlayerChipLedgerEntries.SingleAsync(entry =>
			entry.PlayerId == playerId &&
			entry.Type == PlayerChipLedgerEntryType.CashOut);

		cashOutEntry.AmountDelta.Should().Be(0);
		cashOutEntry.BalanceAfter.Should().Be(100_000);
		cashOutEntry.ReferenceId.Should().Be(gameId);
		cashOutEntry.Reason.Should().Contain("in-game balance: 425");
	}

	[Fact]
	public async Task RecordHandSettlementAsync_WhenSettlementAlreadyExists_DoesNotApplyDuplicateBalanceChange()
	{
		const int originalDelta = 125;
		var playerId = Guid.NewGuid();
		var gameId = Guid.NewGuid();
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();

		await using (var firstContext = CreateContext(databaseName, databaseRoot))
		{
			var firstSut = new PlayerChipWalletService(firstContext);

			await firstSut.RecordHandSettlementAsync(
				playerId,
				originalDelta,
				gameId,
				3,
				"actor-user",
				CancellationToken.None);

			await firstContext.SaveChangesAsync();
		}

		await using var secondContext = CreateContext(databaseName, databaseRoot);
		var secondSut = new PlayerChipWalletService(secondContext);

		await secondSut.RecordHandSettlementAsync(
			playerId,
			250,
			gameId,
			3,
			"actor-user",
			CancellationToken.None);

		await secondContext.SaveChangesAsync();

		var account = await secondContext.PlayerChipAccounts.SingleAsync(entry => entry.PlayerId == playerId);
		account.Balance.Should().Be(100_000 + originalDelta);

		var settlementEntries = await secondContext.PlayerChipLedgerEntries
			.Where(entry =>
				entry.PlayerId == playerId &&
				entry.ReferenceId == gameId &&
				entry.HandNumber == 3 &&
				entry.Type == PlayerChipLedgerEntryType.HandSettlement)
			.ToListAsync();

		settlementEntries.Should().ContainSingle();
		settlementEntries[0].AmountDelta.Should().Be(originalDelta);
		settlementEntries[0].BalanceAfter.Should().Be(100_000 + originalDelta);
	}

	[Fact]
	public async Task GetBalanceAsync_WhenTwoRelationalContextsInitializeSameAccount_UsesPersistedWinner()
	{
		var playerId = Guid.NewGuid();
		var databasePath = Path.Combine(Path.GetTempPath(), $"wallet-{Guid.NewGuid():N}.db");
		var setupConnectionString = new SqliteConnectionStringBuilder
		{
			DataSource = databasePath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			ForeignKeys = false
		}.ToString();
		var entrants = 0;
		var readyToSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		try
		{
			await using (var setupContext = CreateSqliteContext(setupConnectionString))
			{
				await setupContext.Database.EnsureCreatedAsync();
			}

			await using var firstContext = CreateCoordinatedSqliteContext(
				setupConnectionString,
				() => CoordinateConcurrentSaveAsync(ref entrants, readyToSave));
			await using var secondContext = CreateCoordinatedSqliteContext(
				setupConnectionString,
				() => CoordinateConcurrentSaveAsync(ref entrants, readyToSave));

			var firstSut = new PlayerChipWalletService(firstContext);
			var secondSut = new PlayerChipWalletService(secondContext);

			var firstTask = firstSut.GetBalanceAsync(playerId, CancellationToken.None);
			var secondTask = secondSut.GetBalanceAsync(playerId, CancellationToken.None);

			var balances = await Task.WhenAll(firstTask, secondTask);

			balances.Should().Equal(100_000, 100_000);

			await using var verificationContext = CreateSqliteContext(setupConnectionString);
			var accounts = await verificationContext.PlayerChipAccounts.ToListAsync();
			var registrationEntries = await verificationContext.PlayerChipLedgerEntries
				.Where(entry => entry.PlayerId == playerId && entry.Type == PlayerChipLedgerEntryType.Add)
				.ToListAsync();

			accounts.Should().ContainSingle();
			accounts[0].PlayerId.Should().Be(playerId);
			accounts[0].Balance.Should().Be(100_000);
			registrationEntries.Should().ContainSingle();
			registrationEntries[0].AmountDelta.Should().Be(100_000);
		}
		finally
		{
			try
			{
				if (File.Exists(databasePath))
				{
					File.Delete(databasePath);
				}
			}
			catch (IOException)
			{
				// SQLite can briefly hold the file after context disposal; a temp-file leak here is acceptable for the race test.
			}
		}
	}

	private static CardsDbContext CreateContext()
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static CardsDbContext CreateSqliteContext(string connectionString)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseSqlite(connectionString)
			.Options;

		return new RelaxedSqliteCardsDbContext(options);
	}

	private static CoordinatedCardsDbContext CreateCoordinatedSqliteContext(
		string connectionString,
		Func<Task> beforeSaveAsync)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseSqlite(connectionString)
			.Options;

		return new CoordinatedCardsDbContext(options, beforeSaveAsync);
	}

	private static Task CoordinateConcurrentSaveAsync(
		ref int entrants,
		TaskCompletionSource readyToSave)
	{
		if (Interlocked.Increment(ref entrants) == 2)
		{
			readyToSave.TrySetResult();
		}

		return readyToSave.Task;
	}

	private class RelaxedSqliteCardsDbContext(DbContextOptions<CardsDbContext> options) : CardsDbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<PlayerChipAccount>()
				.Property(entity => entity.RowVersion)
				.IsRequired(false)
				.ValueGeneratedNever();

			modelBuilder.Entity<PlayerChipLedgerEntry>()
				.Property(entity => entity.RowVersion)
				.IsRequired(false)
				.ValueGeneratedNever();
		}
	}

	private sealed class CoordinatedCardsDbContext(
		DbContextOptions<CardsDbContext> options,
		Func<Task> beforeSaveAsync) : RelaxedSqliteCardsDbContext(options)
	{
		public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		{
			if (ChangeTracker.Entries<PlayerChipAccount>().Any(entry => entry.State == EntityState.Added))
			{
				await beforeSaveAsync();
			}

			return await base.SaveChangesAsync(cancellationToken);
		}
	}
}