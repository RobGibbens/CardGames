using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace CardGames.IntegrationTests.Games.HoldEm;

/// <summary>
///     Hold'em showdown persistence against SQL Server. Each seat requests showdown when the
///     hand ends, and the in-memory provider does not enforce the rowversion conflict that produces.
/// </summary>
public class HoldEmShowdownSqlTests : IAsyncLifetime
{
	private MsSqlContainer _sql = null!;

	public async Task InitializeAsync()
	{
		_sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
		await _sql.StartAsync();
	}

	public async Task DisposeAsync()
	{
		await _sql.DisposeAsync();
	}

	[Fact]
	public async Task PerformShowdown_WhenTwoSeatsFinishTogether_BothSucceedAndSettleOnce()
	{
		await using var seed = CreateContext();
		await seed.Database.EnsureCreatedAsync();
		var gameId = await SeedShowdownAsync(seed);

		var holdSaves = new HoldSavesUntilBothArrive();
		var results = await Task.WhenAll(
			RunShowdownAsync(gameId, holdSaves),
			RunShowdownAsync(gameId, holdSaves));

		results.Should().AllSatisfy(result =>
			result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "showdown should succeed"));

		await using var verify = CreateContext();
		var game = await verify.Games.AsNoTracking().SingleAsync(candidate => candidate.Id == gameId);
		game.CurrentPhase.Should().Be("Complete");

		var pots = await verify.Pots.AsNoTracking()
			.Where(pot => pot.GameId == gameId && pot.HandNumber == 1)
			.ToListAsync();
		pots.Should().OnlyContain(pot => pot.IsAwarded);
		pots.Sum(pot => pot.Amount).Should().Be(100);

		var stacks = await verify.GamePlayers.AsNoTracking()
			.Where(player => player.GameId == gameId)
			.Select(player => player.ChipStack)
			.ToListAsync();
		stacks.Sum().Should().Be(2000);

		var settlements = await verify.PlayerChipLedgerEntries.AsNoTracking()
			.Where(entry => entry.ReferenceId == gameId && entry.Type == PlayerChipLedgerEntryType.HandSettlement)
			.ToListAsync();
		settlements.Should().HaveCount(2);
		settlements.Sum(entry => entry.AmountDelta).Should().Be(0);
	}

	private async Task<OneOf.OneOf<PerformShowdownSuccessful, PerformShowdownError>> RunShowdownAsync(
		Guid gameId,
		HoldSavesUntilBothArrive holdSaves)
	{
		await using var context = CreateContext(holdSaves);
		var handler = new PerformShowdownCommandHandler(
			context,
			new FakeHandHistoryRecorder(),
			new HandSettlementService(new PlayerChipWalletService(context)));

		try
		{
			return await handler.Handle(new PerformShowdownCommand(gameId), CancellationToken.None);
		}
		catch (DbUpdateConcurrencyException ex)
		{
			var entries = string.Join(", ", ex.Entries.Select(entry => entry.Metadata.ClrType.Name));
			throw new Exception($"Concurrency entries: {entries}", ex);
		}
	}

	private async Task<Guid> SeedShowdownAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Code = "HOLDEM",
			Name = "Texas Hold'em",
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			MaxCommunityCards = 5,
			MaxPlayerCards = 7,
			BettingStructure = BettingStructure.Blinds
		};
		var alice = new Player { Name = "Alice", Email = $"alice-{Guid.NewGuid():N}@test.com" };
		var bob = new Player { Name = "Bob", Email = $"bob-{Guid.NewGuid():N}@test.com" };
		var game = new Game
		{
			GameType = gameType,
			Name = "Hold'em SQL",
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			SmallBlind = 5,
			BigBlind = 10,
			CreatedAt = now,
			UpdatedAt = now
		};
		var aliceSeat = new GamePlayer
		{
			Game = game,
			Player = alice,
			SeatPosition = 0,
			ChipStack = 950,
			TotalContributedThisHand = 50,
			Status = GamePlayerStatus.Active
		};
		var bobSeat = new GamePlayer
		{
			Game = game,
			Player = bob,
			SeatPosition = 1,
			ChipStack = 950,
			TotalContributedThisHand = 50,
			Status = GamePlayerStatus.Active
		};
		var pot = new CardGames.Poker.Api.Data.Entities.Pot
		{
			Game = game,
			HandNumber = 1,
			PotOrder = 0,
			PotType = PotType.Main,
			Amount = 100,
			CreatedAt = now
		};
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = aliceSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = now
		});
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = bobSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = now
		});

		context.AddRange(
			gameType, alice, bob, game, aliceSeat, bobSeat, pot,
			new PlayerChipAccount
			{
				Player = alice,
				Balance = 10_000,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			},
			new PlayerChipAccount
			{
				Player = bob,
				Balance = 10_000,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			},
			Hole(game, aliceSeat, CardSuit.Spades, CardSymbol.Ace, 1, now),
			Hole(game, aliceSeat, CardSuit.Hearts, CardSymbol.King, 2, now),
			Hole(game, bobSeat, CardSuit.Clubs, CardSymbol.Queen, 1, now),
			Hole(game, bobSeat, CardSuit.Diamonds, CardSymbol.Jack, 2, now),
			Community(game, CardSuit.Hearts, CardSymbol.Deuce, 1, now),
			Community(game, CardSuit.Clubs, CardSymbol.Three, 2, now),
			Community(game, CardSuit.Diamonds, CardSymbol.Four, 3, now),
			Community(game, CardSuit.Spades, CardSymbol.Five, 4, now),
			Community(game, CardSuit.Hearts, CardSymbol.Nine, 5, now));
		await context.SaveChangesAsync();
		return game.Id;
	}

	private CardsDbContext CreateContext(HoldSavesUntilBothArrive? holdSaves = null)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseSqlServer(_sql.GetConnectionString());
		if (holdSaves is not null)
			options.AddInterceptors(holdSaves);
		return new CardsDbContext(options.Options);
	}

	private sealed class HoldSavesUntilBothArrive : SaveChangesInterceptor
	{
		private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _arrived;

		public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
			DbContextEventData eventData,
			InterceptionResult<int> result,
			CancellationToken cancellationToken = default)
		{
			if (Interlocked.Increment(ref _arrived) >= 2)
				_bothArrived.TrySetResult();

			await _bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
			return await base.SavingChangesAsync(eventData, result, cancellationToken);
		}
	}

	private static GameCard Hole(Game game, GamePlayer player, CardSuit suit, CardSymbol symbol, int dealOrder,
		DateTimeOffset dealtAt)
	{
		return new GameCard
		{
			Game = game,
			GamePlayer = player,
			HandNumber = 1,
			Suit = suit,
			Symbol = symbol,
			Location = CardLocation.Hand,
			DealOrder = dealOrder,
			DealtAtPhase = nameof(Phases.PreFlop),
			DealtAt = dealtAt
		};
	}

	private static GameCard Community(Game game, CardSuit suit, CardSymbol symbol, int dealOrder,
		DateTimeOffset dealtAt)
	{
		return new GameCard
		{
			Game = game,
			HandNumber = 1,
			Suit = suit,
			Symbol = symbol,
			Location = CardLocation.Community,
			IsVisible = true,
			DealOrder = dealOrder,
			DealtAtPhase = nameof(Phases.Flop),
			DealtAt = dealtAt
		};
	}
}
