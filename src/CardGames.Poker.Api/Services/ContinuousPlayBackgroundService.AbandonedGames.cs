using System.Diagnostics;
using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Contracts.SignalR;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Services;

public sealed partial class ContinuousPlayBackgroundService
{
	/// <summary>
	/// Checks for games where all players have left after the game started,
	/// and marks them as complete.
	/// </summary>
	private async Task ProcessAbandonedGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		LeagueGameCompletionSyncService? leagueCompletionSync,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		// Find in-progress games (not waiting phases)
		var inProgressPhases = new[]
		{
					nameof(Phases.CollectingAntes),
					nameof(Phases.Dealing),
					nameof(Phases.FirstBettingRound),
					nameof(Phases.DrawPhase),
					nameof(Phases.SecondBettingRound),
					nameof(Phases.Showdown),
					nameof(Phases.Complete),
					nameof(Phases.WaitingForDealerChoice),
					// Hold'Em / Omaha community-card phases
					"CollectingBlinds",
					"PreFlop",
					"Flop",
					"Turn",
					"River",
					// Seven Card Stud street phases
					"ThirdStreet",
					"FourthStreet",
					"FifthStreet",
					"SixthStreet",
					"SeventhStreet",
					// Klondike reveal phase
					"KlondikeReveal"
				};

		var activeGames = await context.Games
			.Where(g => inProgressPhases.Contains(g.CurrentPhase) &&
						(g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands))
			.Include(g => g.GamePlayers)
			.ToListAsync(cancellationToken);

		foreach (var game in activeGames)
		{
			using var activity = StartContinuousPlayActivity(game, PhaseAbandoned);
			try
			{
				// Check if all players have left (no active, connected players remaining)
				var activePlayers = game.GamePlayers
					.Where(gp => gp.Status == GamePlayerStatus.Active &&
								 gp.LeftAtHandNumber == -1)
					.ToList();

				if (activePlayers.Count == 0)
				{
					_logger.LogInformation(
						"Game {GameId} has no remaining players, marking as complete",
						game.Id);

			game.CurrentPhase = nameof(Phases.Complete);
			game.Status = GameStatus.Completed;
			game.EndedAt = now;
			game.UpdatedAt = now;
			game.NextHandStartsAt = null;
			game.HandCompletedAt = null;
			game.IsPausedForChipCheck = false;
			game.ChipCheckPauseStartedAt = null;
			game.ChipCheckPauseEndsAt = null;
			game.IsPausedForRebuyGrace = false;
			game.RebuyGraceStartedAt = null;
			game.RebuyGraceEndsAt = null;

				await context.SaveChangesAsync(cancellationToken);
				if (leagueCompletionSync is not null)
				{
					await leagueCompletionSync.SyncLeagueEventCompletionAsync(game.Id, cancellationToken);
				}
				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
					RecordGameProcessed(PhaseAbandoned, OutcomeAdvanced);
				}
				else
				{
					RecordGameProcessed(PhaseAbandoned, OutcomeSkipped);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseAbandoned, OutcomeFailed);
				throw;
			}
		}
	}
}
