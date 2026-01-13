using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.KingsAndLows;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

/// <summary>
/// Handles the <see cref="DropOrStayCommand"/> to record a player's drop or stay decision.
/// </summary>
public class DropOrStayCommandHandler(CardsDbContext context)
	: IRequestHandler<DropOrStayCommand, OneOf<DropOrStaySuccessful, DropOrStayError>>
{
	public async Task<OneOf<DropOrStaySuccessful, DropOrStayError>> Handle(
		DropOrStayCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new DropOrStayError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = DropOrStayErrorCode.GameNotFound
			};
		}

		// 2. Validate game is in DropOrStay phase
		if (game.CurrentPhase != nameof(Phases.DropOrStay))
		{
			return new DropOrStayError
			{
				Message = $"Cannot make drop/stay decision. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(Phases.DropOrStay)}' phase.",
				Code = DropOrStayErrorCode.InvalidPhase
			};
		}

		// 3. Find the player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new DropOrStayError
			{
				Message = $"Player with ID '{command.PlayerId}' is not in this game.",
				Code = DropOrStayErrorCode.PlayerNotFound
			};
		}

		// 4. Check if player has already decided
		if (gamePlayer.DropOrStayDecision.HasValue &&
			gamePlayer.DropOrStayDecision.Value != Data.Entities.DropOrStayDecision.Undecided)
		{
			return new DropOrStayError
			{
				Message = "Player has already made their decision.",
				Code = DropOrStayErrorCode.AlreadyDecided
			};
		}

		// 5. Parse and validate decision
		Data.Entities.DropOrStayDecision decision;
		if (string.Equals(command.Decision, "Drop", StringComparison.OrdinalIgnoreCase))
		{
			decision = Data.Entities.DropOrStayDecision.Drop;
		}
		else if (string.Equals(command.Decision, "Stay", StringComparison.OrdinalIgnoreCase))
		{
			decision = Data.Entities.DropOrStayDecision.Stay;
		}
		else
		{
			return new DropOrStayError
			{
				Message = $"Invalid decision '{command.Decision}'. Must be 'Drop' or 'Stay'.",
				Code = DropOrStayErrorCode.InvalidDecision
			};
		}

		// 6. Record the decision
		gamePlayer.DropOrStayDecision = decision;
		if (decision == Data.Entities.DropOrStayDecision.Drop)
		{
			gamePlayer.HasFolded = true;
		}

		// 6.5. Explicitly save changes here to ensure the decision is persisted before checking allDecided
		// This handles concurrency issues where EF might not have updated the local collection yet
		await context.SaveChangesAsync(cancellationToken);

		// Re-fetch players to ensure we have the latest state including other concurrent updates
		// and the current player's just-saved decision
		var refreshedGame = await context.Games
			.Include(g => g.GamePlayers)
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);
            
        if (refreshedGame == null) return new DropOrStayError { Message = "Game lost", Code = DropOrStayErrorCode.GameNotFound };

		// 7. Check if all players have decided
		var activePlayers = refreshedGame.GamePlayers
			.Where(gp => gp is { Status: GamePlayerStatus.Active, IsSittingOut: false, HasFolded: false })
			.ToList();

		var allDecided = activePlayers.All(gp =>
			gp.DropOrStayDecision.HasValue &&
			gp.DropOrStayDecision.Value != Data.Entities.DropOrStayDecision.Undecided);

		var stayingPlayers = activePlayers
			.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
			.ToList();

		var droppedPlayers = activePlayers
			.Where(gp => gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Drop)
			.ToList();

		string? nextPhase = null;

		// 8. If all players have decided, advance to next phase
		if (allDecided)
		{
			if (stayingPlayers.Count == 0)
			{
				// All players dropped - dead hand
				game.CurrentPhase = nameof(Phases.Complete);
				game.HandCompletedAt = now;
				game.NextHandStartsAt = now.AddSeconds(ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);
				MoveDealer(game);
				nextPhase = game.CurrentPhase;
			}
			else if (stayingPlayers.Count == 1)
			{
				// Single player stayed - go to draw phase (then player vs deck)
				game.CurrentPhase = nameof(Phases.DrawPhase);
				game.CurrentDrawPlayerIndex = stayingPlayers[0].SeatPosition;
				game.CurrentPlayerIndex = stayingPlayers[0].SeatPosition;
				nextPhase = game.CurrentPhase;
			}
			else
			{
				// Multiple players stayed - go to draw phase
				game.CurrentPhase = nameof(Phases.DrawPhase);
				// Find first staying player after dealer (order by SeatPosition for consistent indexing)
				var dealerSeatPosition = game.DealerPosition;
				var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

				// Find the index of the dealer in the ordered player list
				var dealerIndex = gamePlayersList.FindIndex(gp => gp.SeatPosition == dealerSeatPosition);
				if (dealerIndex < 0)
				{
					// Dealer seat not occupied, start from seat position 0
					dealerIndex = 0;
				}

				var nextPlayerIndex = (dealerIndex + 1) % gamePlayersList.Count;
				var searched = 0;
				while (searched < gamePlayersList.Count)
				{
					var player = gamePlayersList[nextPlayerIndex];
					if (player.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
					{
						game.CurrentDrawPlayerIndex = player.SeatPosition;
						game.CurrentPlayerIndex = player.SeatPosition;
						break;
					}
					nextPlayerIndex = (nextPlayerIndex + 1) % gamePlayersList.Count;
					searched++;
				}
				nextPhase = game.CurrentPhase;
			}
		}

		game.UpdatedAt = now;

		// 9. Persist changes
		await context.SaveChangesAsync(cancellationToken);

		return new DropOrStaySuccessful
		{
			GameId = game.Id,
			PlayerId = command.PlayerId,
			Decision = command.Decision,
			AllPlayersDecided = allDecided,
			StayingCount = stayingPlayers.Count,
			DroppedCount = droppedPlayers.Count,
			NextPhase = nextPhase
		};
	}

	/// <summary>
	/// Moves the dealer button to the next occupied seat position (clockwise).
	/// </summary>
	private static void MoveDealer(Game game)
	{
		var occupiedSeats = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active)
			.OrderBy(gp => gp.SeatPosition)
			.Select(gp => gp.SeatPosition)
			.ToList();

		if (occupiedSeats.Count == 0)
		{
			return;
		}

		var currentPosition = game.DealerPosition;

		// Find next occupied seat clockwise from current position
		var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

		if (seatsAfterCurrent.Count > 0)
		{
			game.DealerPosition = seatsAfterCurrent.First();
		}
		else
		{
			game.DealerPosition = occupiedSeats.First();
		}
	}
}
