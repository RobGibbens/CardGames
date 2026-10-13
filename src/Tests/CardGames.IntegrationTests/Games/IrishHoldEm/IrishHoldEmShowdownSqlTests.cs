using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Evaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace CardGames.IntegrationTests.Games.IrishHoldEm;

/// <summary>
///     Showdown persistence against SQL Server. The in-memory provider does not enforce rowversion
///     concurrency, which is where this save fails.
/// </summary>
public class IrishHoldEmShowdownSqlTests : IAsyncLifetime
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
	public async Task PerformShowdown_WithContributionRows_SavesOnSqlServer()
	{
		await using var context = CreateContext();
		await context.Database.EnsureCreatedAsync();

		var gameType = new GameType
		{
			Code = "IRISHHOLDEM",
			Name = "Irish Hold 'Em",
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 4,
			MaxCommunityCards = 5,
			MaxPlayerCards = 9,
			BettingStructure = BettingStructure.Blinds
		};
		context.GameTypes.Add(gameType);

		var alice = new Player { Name = "Alice", Email = "alice@test.com" };
		var bob = new Player { Name = "Bob", Email = "bob@test.com" };
		var game = new Game
		{
			GameType = gameType,
			Name = "Irish SQL",
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			SmallBlind = 5,
			BigBlind = 10,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
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
			CreatedAt = DateTimeOffset.UtcNow
		};
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = aliceSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = DateTimeOffset.UtcNow
		});
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = bobSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = DateTimeOffset.UtcNow
		});

		var dealtAt = DateTimeOffset.UtcNow;
		context.AddRange(
			alice, bob, game, aliceSeat, bobSeat, pot,
			Hole(game, aliceSeat, CardSuit.Spades, CardSymbol.Ace, 1, dealtAt),
			Hole(game, aliceSeat, CardSuit.Hearts, CardSymbol.King, 2, dealtAt),
			Hole(game, bobSeat, CardSuit.Clubs, CardSymbol.Queen, 1, dealtAt),
			Hole(game, bobSeat, CardSuit.Diamonds, CardSymbol.Jack, 2, dealtAt),
			Community(game, CardSuit.Hearts, CardSymbol.Deuce, 1, dealtAt),
			Community(game, CardSuit.Clubs, CardSymbol.Three, 2, dealtAt),
			Community(game, CardSuit.Diamonds, CardSymbol.Four, 3, dealtAt),
			Community(game, CardSuit.Spades, CardSymbol.Five, 4, dealtAt),
			Community(game, CardSuit.Hearts, CardSymbol.Nine, 5, dealtAt));
		await context.SaveChangesAsync();
		context.ChangeTracker.Clear();

		var handler = new PerformShowdownCommandHandler(
			context,
			new GameFlowHandlerFactory(),
			new HandEvaluatorFactory(),
			new FakeHandHistoryRecorder(),
			new FakeHandSettlementService(),
			NullLogger<PerformShowdownCommandHandler>.Instance);

		try
		{
			var result = await handler.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);
			result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "showdown should succeed");
		}
		catch (DbUpdateConcurrencyException ex)
		{
			var entries = string.Join(", ", ex.Entries.Select(entry => entry.Metadata.Name));
			throw new Exception($"Concurrency entries: {entries}", ex);
		}

		var completed = await CreateContext().Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);
		completed.CurrentPhase.Should().Be(nameof(Phases.Complete));
	}

	[Fact]
	public async Task PerformShowdown_WhenTwoSeatsFinishTogether_BothSucceed()
	{
		await using var seed = CreateContext();
		await seed.Database.EnsureCreatedAsync();
		var gameId = await SeedShowdownAsync(seed);

		var holdSaves = new HoldSavesUntilBothArrive();
		var first = RunShowdownAsync(gameId, holdSaves);
		var second = RunShowdownAsync(gameId, holdSaves);
		var results = await Task.WhenAll(first, second);

		results.Should().AllSatisfy(result =>
			result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : "showdown should succeed"));

		await using var verify = CreateContext();
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
	}

	private async Task<OneOf.OneOf<PerformShowdownSuccessful, PerformShowdownError>> RunShowdownAsync(
		Guid gameId,
		HoldSavesUntilBothArrive holdSaves)
	{
		await using var context = CreateContext(holdSaves);
		var handler = new PerformShowdownCommandHandler(
			context,
			new GameFlowHandlerFactory(),
			new HandEvaluatorFactory(),
			new FakeHandHistoryRecorder(),
			new HandSettlementService(new PlayerChipWalletService(context)),
			NullLogger<PerformShowdownCommandHandler>.Instance);

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
		var gameType = new GameType
		{
			Code = "IRISHHOLDEM",
			Name = "Irish Hold 'Em",
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 4,
			MaxCommunityCards = 5,
			MaxPlayerCards = 9,
			BettingStructure = BettingStructure.Blinds
		};
		var alice = new Player { Name = "Alice", Email = $"alice-{Guid.NewGuid():N}@test.com" };
		var bob = new Player { Name = "Bob", Email = $"bob-{Guid.NewGuid():N}@test.com" };
		var game = new Game
		{
			GameType = gameType,
			Name = "Irish SQL",
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			SmallBlind = 5,
			BigBlind = 10,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
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
			CreatedAt = DateTimeOffset.UtcNow
		};
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = aliceSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = DateTimeOffset.UtcNow
		});
		pot.Contributions.Add(new PotContribution
		{
			GamePlayer = bobSeat,
			Amount = 50,
			IsEligibleToWin = true,
			ContributedAt = DateTimeOffset.UtcNow
		});

		var dealtAt = DateTimeOffset.UtcNow;
		context.AddRange(
			gameType, alice, bob, game, aliceSeat, bobSeat, pot,
			Hole(game, aliceSeat, CardSuit.Spades, CardSymbol.Ace, 1, dealtAt),
			Hole(game, aliceSeat, CardSuit.Hearts, CardSymbol.King, 2, dealtAt),
			Hole(game, bobSeat, CardSuit.Clubs, CardSymbol.Queen, 1, dealtAt),
			Hole(game, bobSeat, CardSuit.Diamonds, CardSymbol.Jack, 2, dealtAt),
			Community(game, CardSuit.Hearts, CardSymbol.Deuce, 1, dealtAt),
			Community(game, CardSuit.Clubs, CardSymbol.Three, 2, dealtAt),
			Community(game, CardSuit.Diamonds, CardSymbol.Four, 3, dealtAt),
			Community(game, CardSuit.Spades, CardSymbol.Five, 4, dealtAt),
			Community(game, CardSuit.Hearts, CardSymbol.Nine, 5, dealtAt));
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
