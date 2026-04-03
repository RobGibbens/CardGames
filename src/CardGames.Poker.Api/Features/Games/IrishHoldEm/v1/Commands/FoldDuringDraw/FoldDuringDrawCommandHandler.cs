using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;

/// <summary>
/// Handles the <see cref="FoldDuringDrawCommand"/> to fold a player during the Irish Hold 'Em
/// discard phase (typically when their turn timer expires).
/// </summary>
public class FoldDuringDrawCommandHandler(
	CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager,
	ILogger<FoldDuringDrawCommandHandler> logger)
	: IRequestHandler<FoldDuringDrawCommand, OneOf<FoldDuringDrawSuccessful, FoldDuringDrawError>>
{
	public async Task<OneOf<FoldDuringDrawSuccessful, FoldDuringDrawError>> Handle(
		FoldDuringDrawCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new FoldDuringDrawError { Message = $"Game with ID '{command.GameId}' was not found." };
		}

		if (game.CurrentPhase != nameof(Phases.DrawPhase))
		{
			return new FoldDuringDrawError
			{
				Message = $"Cannot fold during draw. Game is in '{game.CurrentPhase}' phase."
			};
		}

		var player = game.GamePlayers.FirstOrDefault(gp => gp.SeatPosition == command.PlayerSeatIndex);
		if (player is null)
		{
			return new FoldDuringDrawError { Message = "Player not found at the specified seat." };
		}

		if (player.HasFolded)
		{
			return new FoldDuringDrawError { Message = "Player has already folded." };
		}

		logger.LogInformation(
			"Folding player {PlayerName} (seat {SeatIndex}) during draw phase for game {GameId}",
			player.Player.Name, command.PlayerSeatIndex, command.GameId);

		// Fold the player
		player.HasFolded = true;

		// Find remaining active (non-folded) players
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		var onlyOneRemains = activePlayers.Count <= 1;

		if (onlyOneRemains)
		{
			// Only one player left — skip straight to Showdown
			game.CurrentPhase = nameof(Phases.Showdown);
			game.CurrentPlayerIndex = -1;
			game.CurrentDrawPlayerIndex = -1;

			logger.LogInformation(
				"Only one player remains after fold — advancing to Showdown for game {GameId}",
				command.GameId);
		}
		else
		{
			// Find the next eligible draw player (not folded, not already discarded)
			var nextPlayerIndex = FindNextDrawPlayer(game, activePlayers);

			if (nextPlayerIndex >= 0)
			{
				game.CurrentDrawPlayerIndex = nextPlayerIndex;
				game.CurrentPlayerIndex = nextPlayerIndex;
			}
			else
			{
				// All remaining active players have discarded — advance to DrawComplete.
				// Set DrawCompletedAt so the ContinuousPlayBackgroundService can pick it up
				// and transition to the next phase after a brief display period.
				game.CurrentPhase = nameof(Phases.DrawComplete);
				game.DrawCompletedAt = now;
				game.CurrentDrawPlayerIndex = -1;
				game.CurrentPlayerIndex = -1;

				logger.LogInformation(
					"All remaining players have discarded — advancing to DrawComplete for game {GameId}",
					command.GameId);
			}
		}

		game.UpdatedAt = now;
		await context.SaveChangesAsync(cancellationToken);

		if (engineOptions.Value.Enabled)
			await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);

		return new FoldDuringDrawSuccessful
		{
			GameId = game.Id,
			PlayerName = player.Player.Name,
			PlayerSeatIndex = player.SeatPosition,
			CurrentPhase = game.CurrentPhase,
			OnlyOnePlayerRemains = onlyOneRemains
		};
	}

	private static int FindNextDrawPlayer(Game game, List<GamePlayer> activePlayers)
	{
		var currentIndex = game.CurrentDrawPlayerIndex;

		var eligible = activePlayers
			.Where(p => !p.HasFolded && !p.HasDrawnThisRound)
			.OrderBy(p => p.SeatPosition)
			.ToList();

		if (eligible.Count == 0)
			return -1;

		// Find next after current index, with wrap-around
		var next = eligible.FirstOrDefault(p => p.SeatPosition > currentIndex);
		return next?.SeatPosition ?? eligible.First().SeatPosition;
	}
}
