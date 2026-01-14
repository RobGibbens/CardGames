using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
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
	IGameStateBroadcaster broadcaster,
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

		// Players can only sit out after a game has started
		if (gamePlayer.Game.StartedAt == null)
		{
			return "Cannot sit out before the game has started.";
		}

		// Update the status
		gamePlayer.IsSittingOut = request.IsSittingOut;

		logger.LogInformation(
			"Player {PlayerEmail} set sitting out to {IsSittingOut} in game {GameId}",
			userEmail,
			request.IsSittingOut,
			request.GameId);

		await context.SaveChangesAsync(cancellationToken);

		// Broadcast updated state to all players
		await broadcaster.BroadcastGameStateAsync(request.GameId, cancellationToken);

		return true;
	}
}

