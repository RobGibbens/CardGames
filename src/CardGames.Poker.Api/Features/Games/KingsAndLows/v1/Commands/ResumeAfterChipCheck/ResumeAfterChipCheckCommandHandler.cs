using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;

/// <summary>
/// Handles the <see cref="ResumeAfterChipCheckCommand"/> to resume a game after the chip check pause.
/// </summary>
public class ResumeAfterChipCheckCommandHandler(CardsDbContext context, ILogger<ResumeAfterChipCheckCommandHandler> logger)
	: IRequestHandler<ResumeAfterChipCheckCommand, OneOf<ResumeAfterChipCheckSuccessful, ResumeAfterChipCheckError>>
{
	public async Task<OneOf<ResumeAfterChipCheckSuccessful, ResumeAfterChipCheckError>> Handle(
		ResumeAfterChipCheckCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ResumeAfterChipCheckError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ResumeAfterChipCheckErrorCode.GameNotFound
			};
		}

		// 2. Check if the game is actually paused for chip check
		if (!game.IsPausedForChipCheck)
		{
			return new ResumeAfterChipCheckError
			{
				Message = "Game is not currently paused for chip check.",
				Code = ResumeAfterChipCheckErrorCode.GameNotPaused
			};
		}

		// 3. Calculate current pot to check chip coverage
		var currentPotAmount = await context.Pots
			.Where(p => p.GameId == game.Id && !p.IsAwarded)
			.SumAsync(p => p.Amount, cancellationToken);

		// 4. Get eligible players and check for shortages
		var ante = game.Ante ?? 0;
		var eligiblePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active &&
						 !gp.IsSittingOut &&
						 (ante == 0 || gp.ChipStack >= ante))
			.ToList();

		var playersNeedingChips = eligiblePlayers
			.Where(p => p.ChipStack < currentPotAmount && !p.AutoDropOnDropOrStay)
			.ToList();

		// 5. Check if we can resume
		var timerExpired = game.ChipCheckPauseEndsAt.HasValue && now >= game.ChipCheckPauseEndsAt.Value;
		var allPlayersReady = playersNeedingChips.Count == 0;

		if (!allPlayersReady && !timerExpired && !command.ForceResume)
		{
			return new ResumeAfterChipCheckError
			{
				Message = $"{playersNeedingChips.Count} player(s) still cannot cover the pot of {currentPotAmount} chips. Wait for timer to expire or for players to add chips.",
				Code = ResumeAfterChipCheckErrorCode.PlayersStillShort
			};
		}

		// 6. Mark short players for auto-drop if resuming with shortages
		var autoDropCount = 0;
		if (!allPlayersReady && (timerExpired || command.ForceResume))
		{
			foreach (var shortPlayer in playersNeedingChips)
			{
				shortPlayer.AutoDropOnDropOrStay = true;
				autoDropCount++;
				logger.LogInformation(
					"Resuming chip check: Player {PlayerId} at seat {SeatPosition} will auto-drop (chips: {ChipStack}, pot: {PotAmount})",
					shortPlayer.PlayerId, shortPlayer.SeatPosition, shortPlayer.ChipStack, currentPotAmount);
			}
		}

		// 7. Clear pause state
		game.IsPausedForChipCheck = false;
		game.ChipCheckPauseStartedAt = null;
		game.ChipCheckPauseEndsAt = null;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		var message = allPlayersReady
			? "All players now have sufficient chips. Game resumed."
			: $"Game resumed. {autoDropCount} player(s) will auto-drop due to insufficient chips.";

		logger.LogInformation(
			"Chip check resumed for game {GameId}: AllPlayersReady={AllReady}, AutoDropCount={AutoDrop}",
			game.Id, allPlayersReady, autoDropCount);

		return new ResumeAfterChipCheckSuccessful
		{
			GameId = game.Id,
			Message = message,
			CurrentPhase = game.CurrentPhase,
			PlayersAutoDropping = autoDropCount
		};
	}
}
