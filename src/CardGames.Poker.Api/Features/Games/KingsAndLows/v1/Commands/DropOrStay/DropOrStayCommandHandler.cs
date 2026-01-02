using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
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
		if (game.CurrentPhase != nameof(KingsAndLowsPhase.DropOrStay))
		{
			return new DropOrStayError
			{
				Message = $"Cannot make drop/stay decision. Game is in '{game.CurrentPhase}' phase, " +
						  $"but must be in '{nameof(KingsAndLowsPhase.DropOrStay)}' phase.",
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

		// 7. Check if all players have decided
		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut)
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
				game.CurrentPhase = nameof(KingsAndLowsPhase.Complete);
				game.HandCompletedAt = now;
				nextPhase = game.CurrentPhase;
			}
			else if (stayingPlayers.Count == 1)
			{
				// Single player stayed - go to draw phase (then player vs deck)
				game.CurrentPhase = nameof(KingsAndLowsPhase.DrawPhase);
				var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
				game.CurrentDrawPlayerIndex = gamePlayersList.IndexOf(stayingPlayers[0]);
				nextPhase = game.CurrentPhase;
			}
			else
			{
				// Multiple players stayed - go to draw phase
				game.CurrentPhase = nameof(KingsAndLowsPhase.DrawPhase);
				// Find first staying player after dealer (order by SeatPosition for consistent indexing)
				var dealerPos = game.DealerPosition;
				var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
				var nextPlayerIndex = (dealerPos + 1) % gamePlayersList.Count;
				var searched = 0;
				while (searched < gamePlayersList.Count)
				{
					var player = gamePlayersList[nextPlayerIndex];
					if (player.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
					{
						game.CurrentDrawPlayerIndex = nextPlayerIndex;
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
}
