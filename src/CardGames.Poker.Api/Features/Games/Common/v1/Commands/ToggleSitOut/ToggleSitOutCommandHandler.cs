using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.InMemoryEngine;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;

/// <summary>
/// Handles the request effectively toggling the player's sit-out status.
/// </summary>
public sealed class ToggleSitOutCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IGameStateBroadcaster broadcaster,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameExecutionCoordinator coordinator,
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

		if (engineOptions.Value.Enabled)
			return await HandleInMemory(command, currentUserName, cancellationToken);

		return await HandleDatabase(command, currentUserName, cancellationToken);
	}

	private async Task<OneOf<ToggleSitOutSuccessful, ToggleSitOutError>> HandleInMemory(
		ToggleSitOutCommand command, string currentUserName, CancellationToken cancellationToken)
	{
		return await coordinator.ExecuteAsync(command.GameId, async (state, ct) =>
		{
			var runtimePlayer = state.Players.FirstOrDefault(p =>
				p.PlayerName.Equals(currentUserName, StringComparison.OrdinalIgnoreCase) &&
				p.Status != GamePlayerStatus.Left);

			if (runtimePlayer is null)
			{
				return (OneOf<ToggleSitOutSuccessful, ToggleSitOutError>)new ToggleSitOutError("You are not seated at this table");
			}

			runtimePlayer.IsSittingOut = command.IsSittingOut;

			if (!command.IsSittingOut && state.Status == GameStatus.InProgress)
			{
				var hasCards = state.Cards.Any(c =>
					c.GamePlayerId == runtimePlayer.Id &&
					c.HandNumber == state.CurrentHandNumber);

				if (!hasCards)
				{
					runtimePlayer.HasFolded = true;
				}
			}

			if (!command.IsSittingOut && state.CurrentPhase == nameof(Betting.Phases.WaitingForPlayers))
			{
				state.NextHandStartsAt = DateTimeOffset.UtcNow;
			}

			state.UpdatedAt = DateTimeOffset.UtcNow;

			logger.LogInformation(
				"Player {PlayerName} in game {GameId} set sitting out to {IsSittingOut}",
				currentUserName, state.GameId, command.IsSittingOut);

			return (OneOf<ToggleSitOutSuccessful, ToggleSitOutError>)new ToggleSitOutSuccessful(
				state.GameId,
				command.IsSittingOut,
				command.IsSittingOut ? "You will sit out starting next hand." : "You will join the next hand.");
		}, cancellationToken);
	}

	private async Task<OneOf<ToggleSitOutSuccessful, ToggleSitOutError>> HandleDatabase(
		ToggleSitOutCommand command, string currentUserName, CancellationToken cancellationToken)
	{
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

		// If player is coming back during an active hand, ensure they don't get picked up as active
		// by confirming they are marked as folded if they don't have cards for this hand.
		if (!command.IsSittingOut && game.Status == GameStatus.InProgress)
		{
			var hasCards = await context.GameCards
				.AnyAsync(gc => gc.GameId == game.Id && 
								gc.GamePlayerId == gamePlayer.Id && 
								gc.HandNumber == game.CurrentHandNumber, 
					cancellationToken);

			if (!hasCards)
			{
				gamePlayer.HasFolded = true;
			}
		}

		// If player is coming back and game is waiting for players, schedule next hand check immediately
		if (!command.IsSittingOut && game.CurrentPhase == nameof(Betting.Phases.WaitingForPlayers))
		{
			game.NextHandStartsAt = DateTimeOffset.UtcNow;
		}

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
