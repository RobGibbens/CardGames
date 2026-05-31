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
	/// Processes Kings and Lows games in DrawComplete phase that are ready to transition to Showdown.
	/// This allows players to see their new cards for a few seconds before showdown begins.
	/// </summary>
	private async Task ProcessDrawCompleteGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var drawCompleteDeadline = now.AddSeconds(-DrawCompleteDisplayDurationSeconds);

		// Find games in DrawComplete phase where the display period has expired
		var gamesReadyForShowdown = await context.Games
			.Where(g => g.CurrentPhase == nameof(Phases.DrawComplete) &&
						g.DrawCompletedAt != null &&
						g.DrawCompletedAt <= drawCompleteDeadline &&
						g.Status == GameStatus.InProgress)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.Include(g => g.GameType)
			.ToListAsync(cancellationToken);

		using var scope = _scopeFactory.CreateScope();
		var flowHandlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();

		foreach (var game in gamesReadyForShowdown)
		{
			using var activity = StartContinuousPlayActivity(game, PhaseDrawTransition);
			try
			{
				_logger.LogInformation(
					"Game {GameId} DrawComplete display period expired, transitioning to Showdown (GameType={GameType})",
					game.Id,
					game.GameType?.Code ?? game.CurrentHandGameTypeCode ?? "null");

				// Guard: check if the showdown was already handled by an API call (race condition protection).
				// The web client's PerformShowdown API handler may have already awarded pots and transitioned
				// the game to Complete before the background service gets here.
				var currentHandPots = await context.Pots
					.Where(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber)
					.ToListAsync(cancellationToken);
				var isAlreadyHandled = currentHandPots.Any(p => p.IsAwarded);

				if (isAlreadyHandled)
				{
					// Showdown was already performed — just ensure we're in Complete phase.
					_logger.LogInformation(
						"Game {GameId} showdown already handled (pots awarded), skipping inline showdown",
						game.Id);

					// Reload to pick up any changes made by the API handler
					await context.Entry(game).ReloadAsync(cancellationToken);

					// If the API handler already set Complete, we're done. Otherwise, set it now.
					if (game.CurrentPhase != nameof(Phases.Complete))
					{
						game.CurrentPhase = nameof(Phases.Complete);
						game.UpdatedAt = now;
						await context.SaveChangesAsync(cancellationToken);
					}

					await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
					RecordGameProcessed(PhaseDrawTransition, OutcomeSkipped);
					continue;
				}

				// Use flow handler instead of hardcoded game type check
				var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code ?? game.CurrentHandGameTypeCode);
				var nextPhase = await flowHandler.ProcessDrawCompleteAsync(
					context, game, handHistoryRecorder, now, cancellationToken);

				game.CurrentPhase = nextPhase;
				game.UpdatedAt = now;

				// If transitioning to Showdown and handler supports inline showdown
				if (nextPhase == nameof(Phases.Showdown) && flowHandler.SupportsInlineShowdown)
				{
					var showdownResult = await flowHandler.PerformShowdownAsync(
						context, game, handHistoryRecorder, now, cancellationToken);

					if (showdownResult.IsSuccess)
					{
						var postShowdownPhase = await flowHandler.ProcessPostShowdownAsync(
							context, game, showdownResult, now, cancellationToken);

						game.CurrentPhase = postShowdownPhase;

						// Log whether a next-hand pot was created (for multi-hand continuation, e.g. K&L pot matching)
						var nextHandPot = context.ChangeTracker.Entries<Pot>()
							.FirstOrDefault(e => e.Entity.GameId == game.Id &&
							                     e.Entity.HandNumber == game.CurrentHandNumber + 1 &&
							                     e.Entity.PotType == PotType.Main);
						_logger.LogInformation(
							"[SHOWDOWN] Game {GameId}: Inline showdown succeeded. NextHandPot={HasPot} (Amount={Amount}), Winners={Winners}, Losers={Losers}",
							game.Id,
							nextHandPot is not null,
							nextHandPot?.Entity.Amount ?? 0,
							showdownResult.WinnerPlayerIds?.Count ?? 0,
							showdownResult.LoserPlayerIds?.Count ?? 0);
					}
					else
					{
						_logger.LogWarning(
							"Game {GameId} inline showdown failed: {Error}. Phase remains {Phase}",
							game.Id,
							showdownResult.ErrorMessage,
							game.CurrentPhase);
					}
				}

				await context.SaveChangesAsync(cancellationToken);
				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
				RecordGameProcessed(PhaseDrawTransition, OutcomeAdvanced);
			}
			catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseDrawTransition, OutcomeFailed);
				// Concurrency conflict — another handler (e.g., the API PerformShowdown endpoint)
				// modified the game simultaneously. Reload and check if the work is already done.
				_logger.LogWarning(
					ex,
					"Concurrency conflict processing DrawComplete for game {GameId}. Reloading to check if showdown was handled.",
					game.Id);

				try
				{
					await context.Entry(game).ReloadAsync(cancellationToken);

					// If the game is already past DrawComplete (API handler handled it), broadcast and move on
					if (game.CurrentPhase != nameof(Phases.DrawComplete))
					{
						_logger.LogInformation(
							"Game {GameId} was already transitioned to {Phase} by another handler",
							game.Id,
							game.CurrentPhase);
						await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
					}
				}
				catch (Exception reloadEx)
				{
					_logger.LogError(reloadEx, "Failed to reload game {GameId} after concurrency conflict", game.Id);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseDrawTransition, OutcomeFailed);
				_logger.LogError(ex, "Failed to process DrawComplete for game {GameId}", game.Id);
			}
		}
	}

	/// <summary>
	/// Processes Klondike games in KlondikeReveal phase that are ready to transition to Showdown.
	/// The Klondike wild card was revealed when entering this phase; after the display period
	/// expires, the game transitions to Showdown so all players see the overlay simultaneously.
	/// </summary>
	private async Task ProcessKlondikeRevealGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var klondikeRevealDeadline = now.AddSeconds(-KlondikeRevealDisplayDurationSeconds);

		var gamesReadyForShowdown = await context.Games
			.Where(g => g.CurrentPhase == nameof(Phases.KlondikeReveal) &&
						g.DrawCompletedAt != null &&
						g.DrawCompletedAt <= klondikeRevealDeadline &&
						g.Status == GameStatus.InProgress)
			.Include(g => g.GamePlayers)
			.Include(g => g.GameType)
			.ToListAsync(cancellationToken);

		foreach (var game in gamesReadyForShowdown)
		{
			using var activity = StartContinuousPlayActivity(game, PhaseDrawTransition);
			try
			{
				_logger.LogInformation(
					"Game {GameId} KlondikeReveal display period expired, transitioning to Showdown",
					game.Id);

				game.CurrentPhase = nameof(Phases.Showdown);
				game.UpdatedAt = now;

				await context.SaveChangesAsync(cancellationToken);
				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
				RecordGameProcessed(PhaseDrawTransition, OutcomeAdvanced);
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseDrawTransition, OutcomeFailed);
				_logger.LogError(ex, "Failed to process KlondikeReveal for game {GameId}", game.Id);
			}
		}
	}

	/// <summary>
	/// Processes In-Between games where the card-reveal display period has expired.
	/// After a player bets, the third card is shown for <see cref="InBetweenRevealDisplayDurationSeconds"/>
	/// seconds before discarding community cards and advancing to the next player.
	/// </summary>
	private async Task ProcessInBetweenResolutionGamesAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		IHandHistoryRecorder handHistoryRecorder,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var revealDeadline = now.AddSeconds(-InBetweenRevealDisplayDurationSeconds);

		var gamesReady = await context.Games
			.Where(g => g.CurrentPhase == nameof(Phases.InBetweenTurn) &&
						g.DrawCompletedAt != null &&
						g.DrawCompletedAt <= revealDeadline &&
						g.Status == GameStatus.InProgress)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.Include(g => g.Pots)
			.ToListAsync(cancellationToken);

		foreach (var game in gamesReady)
		{
			using var activity = StartContinuousPlayActivity(game, PhaseDrawTransition);
			try
			{
				_logger.LogInformation(
					"Game {GameId} In-Between reveal display period expired, advancing to next player",
					game.Id);

				game.DrawCompletedAt = null;

				await InBetweenFlowHandler.AdvanceToNextPlayerOrCompleteAsync(
					context, game, handHistoryRecorder, now, cancellationToken);

				await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
				RecordGameProcessed(PhaseDrawTransition, OutcomeAdvanced);
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				RecordGameProcessed(PhaseDrawTransition, OutcomeFailed);
				_logger.LogError(ex, "Failed to process In-Between resolution for game {GameId}", game.Id);
			}
		}
	}
}
