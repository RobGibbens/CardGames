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
	private async Task<bool> TryHandleCashGameRebuyGraceAsync(
		CardsDbContext context,
		IGameStateBroadcaster broadcaster,
		LeagueGameCompletionSyncService? leagueCompletionSync,
		IActionTimerService? actionTimerService,
		Game game,
		List<GamePlayer> eligiblePlayers,
		int ante,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (game.TournamentBuyIn.HasValue)
		{
			return false;
		}

		using var activity = StartContinuousPlayActivity(game, PhaseRebuyGrace);
		try
		{
			var bustedPlayers = game.GamePlayers
				.Where(gp => gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut &&
							 gp.LeftAtHandNumber == -1 &&
							 gp.ChipStack <= 0)
				.ToList();

			if (game.IsPausedForRebuyGrace && game.RebuyGraceEndsAt.HasValue)
			{
				if (now < game.RebuyGraceEndsAt.Value)
				{
					if (bustedPlayers.Count > 0)
					{
						await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
						RecordGameProcessed(PhaseRebuyGrace, OutcomeSkipped);
						return true;
					}

					game.IsPausedForChipCheck = false;
					game.ChipCheckPauseStartedAt = null;
					game.ChipCheckPauseEndsAt = null;
					game.IsPausedForRebuyGrace = false;
					game.RebuyGraceStartedAt = null;
					game.RebuyGraceEndsAt = null;
					game.UpdatedAt = now;

					await context.SaveChangesAsync(cancellationToken);
					await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
					RecordGameProcessed(PhaseRebuyGrace, OutcomeAdvanced);
					return false;
				}

				if (eligiblePlayers.Count >= 2)
				{
					SetBustedPlayersToObserve(game.GamePlayers);
					ClearCashGameRebuyGracePause(game, now, scheduleNextHand: true);

					await context.SaveChangesAsync(cancellationToken);
					await broadcaster.BroadcastTableToastAsync(
						new TableToastNotificationDto
						{
							GameId = game.Id,
							Message = "Rebuy timer expired. Resuming play without busted players.",
							Type = "info",
							DurationMs = 5000
						},
						cancellationToken);
					await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
					RecordGameProcessed(PhaseRebuyGrace, OutcomeAdvanced);
					return false;
				}
			}

			if (eligiblePlayers.Count != 1 || bustedPlayers.Count == 0)
			{
				return false;
			}

			// Grace window expired with no successful rebuy; end the game for everyone.
			if (game.IsPausedForRebuyGrace && game.RebuyGraceEndsAt.HasValue && now >= game.RebuyGraceEndsAt.Value)
			{
				_logger.LogInformation(
					"Cash rebuy grace expired for game {GameId}; ending game",
					game.Id);

			game.CurrentPhase = "Ended";
			game.Status = GameStatus.Completed;
			game.EndedAt = now;
			game.HandCompletedAt = now;
			game.NextHandStartsAt = now.AddSeconds(CashRebuyGameOverDisplayDurationSeconds);
			game.IsPausedForChipCheck = false;
			game.ChipCheckPauseStartedAt = null;
			game.ChipCheckPauseEndsAt = null;
			game.IsPausedForRebuyGrace = false;
			game.RebuyGraceStartedAt = null;
			game.RebuyGraceEndsAt = null;
			game.UpdatedAt = now;

			await context.SaveChangesAsync(cancellationToken);
			await SyncLeagueCompletionIfNeededAsync(leagueCompletionSync, game.Id, cancellationToken);
			await broadcaster.BroadcastTableToastAsync(
				new TableToastNotificationDto
				{
					GameId = game.Id,
					Message = "Rebuy timer expired. Game ended.",
					Type = "warning",
					DurationMs = 6000
				},
				cancellationToken);
			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
			RecordGameProcessed(PhaseRebuyGrace, OutcomeAdvanced);
			return true;
		}

		_logger.LogInformation(
			"Cash game {GameId} entering rebuy grace window; funded players: {FundedCount}, busted players: {BustedCount}",
			game.Id,
			eligiblePlayers.Count,
			bustedPlayers.Count);

		game.IsPausedForChipCheck = true;
		game.ChipCheckPauseStartedAt = now;
		game.ChipCheckPauseEndsAt = now.AddSeconds(CashRebuyGraceDurationSeconds);
		game.IsPausedForRebuyGrace = true;
		game.RebuyGraceStartedAt = now;
		game.RebuyGraceEndsAt = now.AddSeconds(CashRebuyGraceDurationSeconds);
		game.CurrentPhase = nameof(Phases.WaitingForPlayers);
		game.NextHandStartsAt = null;
		game.Status = GameStatus.BetweenHands;
		game.UpdatedAt = now;

		var existingCardsToRemove = await context.GameCards
			.Where(gc => gc.GameId == game.Id)
			.ToListAsync(cancellationToken);

		if (existingCardsToRemove.Count > 0)
		{
			context.GameCards.RemoveRange(existingCardsToRemove);
		}

		foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
		{
			gamePlayer.CurrentBet = 0;
			gamePlayer.TotalContributedThisHand = 0;
			gamePlayer.HasFolded = false;
			gamePlayer.IsAllIn = false;
			gamePlayer.HasDrawnThisRound = false;
			gamePlayer.DropOrStayDecision = null;
			gamePlayer.IsSittingOut = gamePlayer.ChipStack < ante || gamePlayer.ChipStack <= 0;
		}

		await context.SaveChangesAsync(cancellationToken);
		actionTimerService?.StartChipCheckPauseTimer(
			game.Id,
			durationSeconds: CashRebuyGraceDurationSeconds,
			onExpired: HandleCashGameRebuyGraceTimerExpiredAsync,
			startedAtUtc: now);
		await broadcaster.BroadcastTableToastAsync(
			new TableToastNotificationDto
			{
				GameId = game.Id,
				Message = $"Rebuy window started: {CashRebuyGraceDurationSeconds} seconds remaining.",
				Type = "info",
				DurationMs = 5000
			},
			cancellationToken);
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
		RecordGameProcessed(PhaseRebuyGrace, OutcomeAdvanced);
		return true;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			RecordGameProcessed(PhaseRebuyGrace, OutcomeFailed);
			throw;
		}
	}

	private async Task HandleCashGameRebuyGraceTimerExpiredAsync(Guid gameId)
	{
		using var scope = _scopeFactory.CreateScope();
		var context = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		var broadcaster = scope.ServiceProvider.GetRequiredService<IGameStateBroadcaster>();
		var leagueCompletionSync = scope.ServiceProvider.GetService<LeagueGameCompletionSyncService>();
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
			.FirstOrDefaultAsync(g => g.Id == gameId, CancellationToken.None);

		if (game is null ||
			!game.IsPausedForRebuyGrace ||
			!game.RebuyGraceEndsAt.HasValue ||
			now < game.RebuyGraceEndsAt.Value)
		{
			return;
		}

		var ante = game.Ante ?? 0;
		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 gp.LeftAtHandNumber == -1 &&
						 (ante == 0 || gp.ChipStack >= ante))
			.ToList();

		if (eligiblePlayers.Count >= 2)
		{
			_logger.LogInformation(
				"Cash rebuy grace timer expired for game {GameId}; resuming play with {EligiblePlayerCount} funded players",
				gameId,
				eligiblePlayers.Count);

			SetBustedPlayersToObserve(game.GamePlayers);
			ClearCashGameRebuyGracePause(game, now, scheduleNextHand: true);

			await context.SaveChangesAsync(CancellationToken.None);
			await broadcaster.BroadcastTableToastAsync(
				new TableToastNotificationDto
				{
					GameId = gameId,
					Message = "Rebuy timer expired. Resuming play without busted players.",
					Type = "info",
					DurationMs = 5000
				},
				CancellationToken.None);
			await broadcaster.BroadcastGameStateAsync(gameId, CancellationToken.None);
			return;
		}

		_logger.LogInformation(
			"Cash rebuy grace timer expired for game {GameId}; ending game",
			gameId);

		game.CurrentPhase = "Ended";
		game.Status = GameStatus.Completed;
		game.EndedAt = now;
		game.HandCompletedAt = now;
		game.NextHandStartsAt = now.AddSeconds(CashRebuyGameOverDisplayDurationSeconds);
		game.IsPausedForChipCheck = false;
		game.ChipCheckPauseStartedAt = null;
		game.ChipCheckPauseEndsAt = null;
		game.IsPausedForRebuyGrace = false;
		game.RebuyGraceStartedAt = null;
		game.RebuyGraceEndsAt = null;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(CancellationToken.None);
		await SyncLeagueCompletionIfNeededAsync(leagueCompletionSync, game.Id, CancellationToken.None);
		await broadcaster.BroadcastTableToastAsync(
			new TableToastNotificationDto
			{
				GameId = gameId,
				Message = "Rebuy timer expired. Game ended.",
				Type = "warning",
				DurationMs = 6000
			},
			CancellationToken.None);
		await broadcaster.BroadcastGameStateAsync(gameId, CancellationToken.None);
	}

	private static void ClearCashGameRebuyGracePause(Game game, DateTimeOffset now, bool scheduleNextHand)
	{
		game.IsPausedForChipCheck = false;
		game.ChipCheckPauseStartedAt = null;
		game.ChipCheckPauseEndsAt = null;
		game.IsPausedForRebuyGrace = false;
		game.RebuyGraceStartedAt = null;
		game.RebuyGraceEndsAt = null;
		game.Status = GameStatus.BetweenHands;
		game.NextHandStartsAt = scheduleNextHand ? now : null;
		game.UpdatedAt = now;
	}
}
