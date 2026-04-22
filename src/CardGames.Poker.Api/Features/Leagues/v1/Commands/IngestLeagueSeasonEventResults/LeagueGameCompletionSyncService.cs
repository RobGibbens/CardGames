using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;

/// <summary>
/// Synchronizes league event state when a linked game completes.
/// When a game marked as Completed is detected, finds any linked league season or one-off event
/// and marks it as Completed, triggering standings updates for season tournaments.
/// </summary>
public sealed class LeagueGameCompletionSyncService
{
	private readonly CardsDbContext _context;
	private readonly ILeagueBroadcaster _leagueBroadcaster;
	private readonly ILogger<LeagueGameCompletionSyncService> _logger;

	public LeagueGameCompletionSyncService(
		CardsDbContext context,
		ILeagueBroadcaster leagueBroadcaster,
		ILogger<LeagueGameCompletionSyncService> logger)
	{
		_context = context;
		_leagueBroadcaster = leagueBroadcaster;
		_logger = logger;
	}

	/// <summary>
	/// Processes a completed game: finds any linked league event and marks it completed,
	/// captures tournament results if applicable, and broadcasts the change.
	/// </summary>
	public async Task SyncLeagueEventCompletionAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		try
		{
			var game = await _context.Games
				.Include(g => g.GamePlayers)
				.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

			if (game is null || game.Status != GameStatus.Completed)
			{
				return;
			}

			var now = DateTimeOffset.UtcNow;

			// Check for linked season event
			var linkedSeasonEvent = await _context.LeagueSeasonEvents
				.FirstOrDefaultAsync(e => e.LaunchedGameId == gameId, cancellationToken);

			if (linkedSeasonEvent is not null)
			{
				if (linkedSeasonEvent.Status != LeagueSeasonEventStatus.Completed)
				{
					linkedSeasonEvent.Status = LeagueSeasonEventStatus.Completed;
					await _context.SaveChangesAsync(cancellationToken);

					// If this is a tournament (has TournamentBuyIn), capture results
					if (linkedSeasonEvent.TournamentBuyIn.HasValue)
					{
						await CaptureSeasonTournamentResultsAsync(
							linkedSeasonEvent.LeagueId,
							linkedSeasonEvent.LeagueSeasonId,
							linkedSeasonEvent.Id,
							game,
							cancellationToken);
					}

					_logger.LogInformation(
						"Marked season event {EventId} as completed for game {GameId}",
						linkedSeasonEvent.Id, gameId);

					await _leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
					{
						LeagueId = linkedSeasonEvent.LeagueId,
						EventId = linkedSeasonEvent.Id,
						SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
						SeasonId = linkedSeasonEvent.LeagueSeasonId,
						ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.ResultsRecorded,
						ChangedAtUtc = now
					}, cancellationToken);
				}

				return;
			}

			// Check for linked one-off event (but don't score it - one-offs don't count toward standings)
			var linkedOneOffEvent = await _context.LeagueOneOffEvents
				.FirstOrDefaultAsync(e => e.LaunchedGameId == gameId, cancellationToken);

			if (linkedOneOffEvent is not null)
			{
				if (linkedOneOffEvent.Status != LeagueOneOffEventStatus.Completed)
				{
					linkedOneOffEvent.Status = LeagueOneOffEventStatus.Completed;
					await _context.SaveChangesAsync(cancellationToken);

					_logger.LogInformation(
						"Marked one-off event {EventId} as completed for game {GameId}",
						linkedOneOffEvent.Id, gameId);

					await _leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
					{
						LeagueId = linkedOneOffEvent.LeagueId,
						EventId = linkedOneOffEvent.Id,
						SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff,
						ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Updated,
						ChangedAtUtc = now
					}, cancellationToken);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error syncing league event completion for game {GameId}", gameId);
		}
	}

	private async Task CaptureSeasonTournamentResultsAsync(
		Guid leagueId,
		Guid seasonId,
		Guid eventId,
		Game game,
		CancellationToken cancellationToken)
	{
		try
		{
			var existingResults = await _context.LeagueSeasonEventResults
				.AsNoTracking()
				.Where(r => r.LeagueSeasonEventId == eventId)
				.AnyAsync(cancellationToken);

			if (existingResults)
			{
				_logger.LogInformation(
					"Tournament results already ingested for event {EventId}; skipping auto-capture",
					eventId);
				return;
			}

			// Derive tournament placements from game state
			// Players ordered by exit time (later exit = better placement)
			// Still-active players are ranked by chip stack (highest first among remaining)
			var placements = DerivePlayerPlacementsFromGameState(game);

			if (placements.Count == 0)
			{
				_logger.LogWarning(
					"No players found in game {GameId} for tournament results; skipping capture",
					game.Id);
				return;
			}

			var now = DateTimeOffset.UtcNow;
			var totalPlayers = placements.Count;

			foreach (var (playerNameOrUserId, placement) in placements)
			{
				// Award points: placement number = points
				// 1st place finisher gets 'totalPlayers' points, last place gets 1 point
				var points = totalPlayers - placement + 1;

				var result = new LeagueSeasonEventResult
				{
					LeagueId = leagueId,
					LeagueSeasonId = seasonId,
					LeagueSeasonEventId = eventId,
					UserId = playerNameOrUserId,
					Placement = placement,
					Points = points,
					ChipsDelta = 0, // Not tracked in automated tournament capture
					RecordedByUserId = "system",
					RecordedAtUtc = now
				};

				_context.LeagueSeasonEventResults.Add(result);
			}

			await _context.SaveChangesAsync(cancellationToken);

			// Recalculate standings for affected players
			var playerIds = placements.Select(p => p.PlayerNameOrUserId).ToArray();
			await LeagueSeasonEventStandingsRecalculator.RebuildForMembersAsync(
				_context, leagueId, playerIds, cancellationToken);

			await _context.SaveChangesAsync(cancellationToken);

			_logger.LogInformation(
				"Captured tournament results for {PlayerCount} players in event {EventId}",
				placements.Count, eventId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error capturing tournament results for event {EventId}", eventId);
		}
	}

	/// <summary>
	/// Derives tournament placements from game state.
	/// Returns list of (player user ID, placement) tuples ordered by placement (1 = best).
	/// </summary>
	private static List<(string PlayerNameOrUserId, int Placement)> DerivePlayerPlacementsFromGameState(Game game)
	{
		var result = new List<(string, int)>();

		// Separate active and eliminated players
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderByDescending(gp => gp.ChipStack) // Highest chip stack is best among active
			.ToList();

		var eliminatedPlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Left || gp.Status == GamePlayerStatus.SittingOut)
			.OrderByDescending(gp => gp.LeftAt ?? gp.JoinedAt) // Later departure is better
			.ToList();

		// Placements: eliminated players are ranked from last to first,
		// then active players are ranked by chips (highest = best overall placement)
		var placement = 1;

		foreach (var player in eliminatedPlayers.Reverse<GamePlayer>())
		{
			result.Add((player.Player?.Id.ToString() ?? $"player-{player.Id}", placement++));
		}

		foreach (var player in activePlayers)
		{
			result.Add((player.Player?.Id.ToString() ?? $"player-{player.Id}", placement++));
		}

		return result;
	}
}
