using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGames.IntegrationTests.Features.Commands;

public sealed class LeagueGameCompletionSyncTests : IAsyncLifetime
{
	private readonly CardsDbContext _context;
	private readonly LeagueGameCompletionSyncService _svc;

	public LeagueGameCompletionSyncTests()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase("test-db-" + Guid.NewGuid())
			.Options;
		_context = new CardsDbContext(options);
		var mockBroadcaster = new MockLeagueBroadcaster();
		_svc = new LeagueGameCompletionSyncService(
			_context,
			mockBroadcaster,
			new MockLogger<LeagueGameCompletionSyncService>());
	}

	public async Task InitializeAsync()
	{
		await _context.Database.EnsureCreatedAsync();
	}

	public async Task DisposeAsync()
	{
		await _context.Database.EnsureDeletedAsync();
		_context.Dispose();
	}

	[Fact]
	public async Task SyncLeagueEventCompletionAsync_MarksSeasonEventCompleted_WhenLinkedGameCompleted()
	{
		// Arrange: Create a league, season, season event, and completed game
		var userId = "test-user";
		var league = new League { Id = Guid.NewGuid(), Name = "Test League", CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
		var season = new LeagueSeason { Id = Guid.NewGuid(), LeagueId = league.Id, Name = "Season 1", CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
		var seasonEvent = new LeagueSeasonEvent
		{
			Id = Guid.NewGuid(),
			LeagueId = league.Id,
			LeagueSeasonId = season.Id,
			Name = "Event 1",
			ScheduledAtUtc = DateTimeOffset.UtcNow,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			CreatedByUserId = userId,
			Status = LeagueSeasonEventStatus.Planned,
			TournamentBuyIn = 100 // Season tournament
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = Guid.NewGuid(),
			Status = GameStatus.Completed,
			CurrentPhase = "Complete",
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow,
			DealerPosition = 0,
			CurrentHandNumber = 1
		};
		seasonEvent.LaunchedGameId = game.Id;

		_context.Leagues.Add(league);
		_context.LeagueSeasons.Add(season);
		_context.LeagueSeasonEvents.Add(seasonEvent);
		_context.Games.Add(game);
		await _context.SaveChangesAsync();

		// Act: Sync the completed game
		await _svc.SyncLeagueEventCompletionAsync(game.Id);

		// Assert: Season event should be marked Completed
		var updatedEvent = await _context.LeagueSeasonEvents.FirstAsync(e => e.Id == seasonEvent.Id);
		Assert.Equal(LeagueSeasonEventStatus.Completed, updatedEvent.Status);
	}

	[Fact]
	public async Task SyncLeagueEventCompletionAsync_MarksOneOffEventCompleted_WhenLinkedGameCompleted()
	{
		// Arrange: Create a league, one-off event, and completed game
		var userId = "test-user";
		var league = new League { Id = Guid.NewGuid(), Name = "Test League", CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
		var oneOffEvent = new LeagueOneOffEvent
		{
			Id = Guid.NewGuid(),
			LeagueId = league.Id,
			Name = "Cash Game",
			CreatedAtUtc = DateTimeOffset.UtcNow,
			CreatedByUserId = userId,
			Status = LeagueOneOffEventStatus.Planned,
			EventType = LeagueOneOffEventType.CashGame // One-off cash game
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = Guid.NewGuid(),
			Status = GameStatus.Completed,
			CurrentPhase = "Complete",
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow,
			DealerPosition = 0,
			CurrentHandNumber = 1
		};
		oneOffEvent.LaunchedGameId = game.Id;

		_context.Leagues.Add(league);
		_context.LeagueOneOffEvents.Add(oneOffEvent);
		_context.Games.Add(game);
		await _context.SaveChangesAsync();

		// Act: Sync the completed game
		await _svc.SyncLeagueEventCompletionAsync(game.Id);

		// Assert: One-off event should be marked Completed
		var updatedEvent = await _context.LeagueOneOffEvents.FirstAsync(e => e.Id == oneOffEvent.Id);
		Assert.Equal(LeagueOneOffEventStatus.Completed, updatedEvent.Status);
	}

	[Fact]
	public async Task SyncLeagueEventCompletionAsync_DoesNothing_WhenGameNotCompleted()
	{
		// Arrange: Create a league, season event, and active game
		var userId = "test-user";
		var league = new League { Id = Guid.NewGuid(), Name = "Test League", CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
		var season = new LeagueSeason { Id = Guid.NewGuid(), LeagueId = league.Id, Name = "Season 1", CreatedByUserId = userId, CreatedAtUtc = DateTimeOffset.UtcNow };
		var seasonEvent = new LeagueSeasonEvent
		{
			Id = Guid.NewGuid(),
			LeagueId = league.Id,
			LeagueSeasonId = season.Id,
			Name = "Event 1",
			ScheduledAtUtc = DateTimeOffset.UtcNow,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			CreatedByUserId = userId,
			Status = LeagueSeasonEventStatus.Planned,
			TournamentBuyIn = 100
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = Guid.NewGuid(),
			Status = GameStatus.WaitingForPlayers, // Not completed
			CurrentPhase = "WaitingToStart",
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow,
			DealerPosition = 0,
			CurrentHandNumber = 1
		};
		seasonEvent.LaunchedGameId = game.Id;

		_context.Leagues.Add(league);
		_context.LeagueSeasons.Add(season);
		_context.LeagueSeasonEvents.Add(seasonEvent);
		_context.Games.Add(game);
		await _context.SaveChangesAsync();

		// Act: Sync the active game
		await _svc.SyncLeagueEventCompletionAsync(game.Id);

		// Assert: Season event should remain Pending
		var updatedEvent = await _context.LeagueSeasonEvents.FirstAsync(e => e.Id == seasonEvent.Id);
		Assert.Equal(LeagueSeasonEventStatus.Planned, updatedEvent.Status);
	}

	[Fact]
	public async Task SyncLeagueEventCompletionAsync_DoesNothing_WhenGameNotFound()
	{
		// Act & Assert: Should not throw
		await _svc.SyncLeagueEventCompletionAsync(Guid.NewGuid());
	}
}

public sealed class MockLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
	public void Log<TState>(
		Microsoft.Extensions.Logging.LogLevel logLevel,
		Microsoft.Extensions.Logging.EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
	}
}

public sealed class MockLeagueBroadcaster : ILeagueBroadcaster
{
	public Task BroadcastLeagueEventChangedAsync(CardGames.Contracts.SignalR.LeagueEventChangedDto eventChanged, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastEventSessionLaunchedAsync(CardGames.Contracts.SignalR.LeagueEventSessionLaunchedDto sessionLaunched, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastJoinRequestSubmittedAsync(CardGames.Contracts.SignalR.LeagueJoinRequestSubmittedDto joinRequestSubmitted, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastJoinRequestUpdatedAsync(CardGames.Contracts.SignalR.LeagueJoinRequestUpdatedDto joinRequestUpdated, string? requesterUserId = null, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}
}
