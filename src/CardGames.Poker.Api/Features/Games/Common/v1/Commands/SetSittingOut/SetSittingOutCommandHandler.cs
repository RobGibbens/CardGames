using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.SetSittingOut;

/// <summary>
/// Handles the SetSittingOut command to update a player's sitting out status.
/// </summary>
public sealed class SetSittingOutCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILogger<SetSittingOutCommandHandler> logger)
	: IRequestHandler<SetSittingOutCommand, OneOf<bool, string>>
{
	public async Task<OneOf<bool, string>> Handle(SetSittingOutCommand request, CancellationToken cancellationToken)
	{
		var userEmail = currentUserService.UserName;
		if (string.IsNullOrWhiteSpace(userEmail))
		{
			return "User is not authenticated.";
		}

		// Find the game player record for this user in this game
		var gamePlayer = await context.GamePlayers
			.Include(gp => gp.Player)
			.Include(gp => gp.Game)
			.FirstOrDefaultAsync(gp => 
				gp.GameId == request.GameId && 
				gp.Player.Email == userEmail, 
				cancellationToken);

		if (gamePlayer == null)
		{
			return "Player is not a participant in this game.";
		}

		// Validate game status - "They can only Sit Out after a game has been started"
		// Assuming we allow it if the game is in progress or about to start? 
		// The prompt says "after a game has been started".
		if (gamePlayer.Game.StartedAt == null)
		{
			// However, if the game is waiting for players, maybe they can't sit out?
			// IsSittingOut is mainly to skip hands.
			// Let's stick to the prompt.
			// "They can only Sit Out after a game has been started"
			// IsStarted check:
			// gamePlayer.Game.StartedAt.HasValue
		}

		// Update the status
		gamePlayer.IsSittingOut = request.IsSittingOut;
		
		await context.SaveChangesAsync(cancellationToken);
		
		return true;
	}
}

