using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;

/// <summary>
/// Handles the request effectively toggling the player's sit-out status.
/// </summary>
public sealed class ToggleSitOutCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IGameStateBroadcaster broadcaster,
	ILogger<ToggleSitOutCommandHandler> logger)
	: IRequestHandler<ToggleSitOutCommand, OneOf<ToggleSitOutSuccessful, ToggleSitOutError>>
{
	public async Task<OneOf<ToggleSitOutSuccessful, ToggleSitOutError>> Handle(
		ToggleSitOutCommand command,
		CancellationToken cancellationToken)
	{
		var currentUserId = currentUserService.UserId;
		var currentUserName = currentUserService.UserName;

		if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(currentUserName))
		{
			return new ToggleSitOutError("User not authenticated");
		}

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game == null)
		{
			return new ToggleSitOutError("Game not found");
		}

		var gamePlayer = game.GamePlayers
			.FirstOrDefault(gp => 
				gp.Player.Name.Equals(currentUserName, StringComparison.OrdinalIgnoreCase) && 
				gp.Status != GamePlayerStatus.Left);

		if (gamePlayer == null)
		{
			return new ToggleSitOutError("You are not seated at this table");
		}

		// Update sit out state
		gamePlayer.IsSittingOut = command.IsSittingOut;

		game.UpdatedAt = DateTimeOffset.UtcNow;
		await context.SaveChangesAsync(cancellationToken);

		// Broadcast update
		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);

		logger.LogInformation(
			"Player {PlayerName} in game {GameId} set sitting out to {IsSittingOut}",
			currentUserName, game.Id, command.IsSittingOut);

		return new ToggleSitOutSuccessful(
			game.Id,
			command.IsSittingOut,
			command.IsSittingOut ? "You will sit out starting next hand." : "You will join the next hand.");
	}
}
