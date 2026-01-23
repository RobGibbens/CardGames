using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Handles the AddChips command to add chips to a player's stack in a game.
/// </summary>
public sealed class AddChipsCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IGameStateBroadcaster broadcaster,
	ILogger<AddChipsCommandHandler> logger)
	: IRequestHandler<AddChipsCommand, OneOf<AddChipsResponse, AddChipsError>>
{
	/// <inheritdoc />
	public async Task<OneOf<AddChipsResponse, AddChipsError>> Handle(
		AddChipsCommand command,
		CancellationToken cancellationToken)
	{
		// Validate amount
		if (command.Amount <= 0)
		{
			return new AddChipsError(
				AddChipsErrorCode.InvalidAmount,
				"Amount must be greater than 0.");
		}

		// Load the game with game type and players
		var game = await context.Games
			.Include(g => g.GameType)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new AddChipsError(
				AddChipsErrorCode.GameNotFound,
				$"Game with ID {command.GameId} was not found.");
		}

		// Check if game has ended
		if (game.Status == GameStatus.Completed || game.Status == GameStatus.Cancelled)
		{
			return new AddChipsError(
				AddChipsErrorCode.GameEnded,
				"This game has ended and chips cannot be added.");
		}

		// Find the game player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
		{
			return new AddChipsError(
				AddChipsErrorCode.PlayerNotInGame,
				"Player is not part of this game.");
		}

		// Determine if chips should be applied immediately
		// Kings and Lows: always immediate
		// Other games: immediate if BetweenHands, otherwise queue
		bool applyImmediately = string.Equals(game.GameType.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(game.CurrentPhase, "BetweenHands", StringComparison.OrdinalIgnoreCase);

		string message;
		if (applyImmediately)
		{
			gamePlayer.ChipStack += command.Amount;
			message = $"{command.Amount} chips added to your stack.";
			logger.LogInformation(
				"Added {Amount} chips immediately to player {PlayerId} in game {GameId}",
				command.Amount, command.PlayerId, command.GameId);
		}
		else
		{
			gamePlayer.PendingChipsToAdd += command.Amount;
			message = $"{command.Amount} chips will be added at the start of the next hand.";
			logger.LogInformation(
				"Queued {Amount} chips for player {PlayerId} in game {GameId} (total pending: {PendingChips})",
				command.Amount, command.PlayerId, command.GameId, gamePlayer.PendingChipsToAdd);
		}

		// Save changes
		await context.SaveChangesAsync(cancellationToken);

		// Broadcast state update
		await broadcaster.BroadcastGameStateAsync(command.GameId, cancellationToken);

		// Broadcast chips added notification (optional)
		// Note: The notification is sent to the specific player via SignalR
		// This is handled separately in the GameStateBroadcaster

		return new AddChipsResponse
		{
			NewChipStack = gamePlayer.ChipStack,
			PendingChipsToAdd = gamePlayer.PendingChipsToAdd,
			AppliedImmediately = applyImmediately,
			Message = message
		};
	}
}
