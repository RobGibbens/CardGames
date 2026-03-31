using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;

/// <summary>
/// Handles the DeleteGameCommand.
/// </summary>
public sealed class DeleteGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILobbyBroadcaster lobbyBroadcaster,
	HybridCache hybridCache,
	ILogger<DeleteGameCommandHandler> logger)
	: IRequestHandler<DeleteGameCommand, OneOf<DeleteGameSuccessful, DeleteGameError>>
{
	/// <inheritdoc />
	public async Task<OneOf<DeleteGameSuccessful, DeleteGameError>> Handle(
		DeleteGameCommand command,
		CancellationToken cancellationToken)
	{
		// 1. Load the game
		var game = await context.Games
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new DeleteGameError
			{
				Code = DeleteGameErrorCode.GameNotFound,
				Message = $"Game with ID {command.GameId} not found."
			};
		}

		// 2. Check if already deleted
		if (game.IsDeleted)
		{
			return new DeleteGameError
			{
				Code = DeleteGameErrorCode.AlreadyDeleted,
				Message = $"Game with ID {command.GameId} has already been deleted."
			};
		}

		// 3. Verify authorization - only the creator can delete
		if (!IsAuthorized(game.CreatedById))
		{
			logger.LogWarning(
				"User {UserId} attempted to delete game {GameId} but is not authorized (creator: {CreatorId})",
				currentUserService.UserId,
				command.GameId,
				game.CreatedById);

			return new DeleteGameError
			{
				Code = DeleteGameErrorCode.NotAuthorized,
				Message = "You are not authorized to delete this game."
			};
		}

		// 4. Perform soft delete
		var now = DateTimeOffset.UtcNow;
		game.IsDeleted = true;
		game.DeletedAt = now;
		game.DeletedById = currentUserService.UserId;
		game.DeletedByName = currentUserService.UserName ?? currentUserService.UserEmail;
		game.UpdatedAt = now;
		game.UpdatedById = currentUserService.UserId;
		game.UpdatedByName = currentUserService.UserName ?? currentUserService.UserEmail;

		await context.SaveChangesAsync(cancellationToken);

		logger.LogInformation(
			"User {UserId} deleted game {GameId} ({GameName})",
			currentUserService.UserId,
			command.GameId,
			game.Name);

		// 5. Broadcast deletion to lobby
		await lobbyBroadcaster.BroadcastGameDeletedAsync(command.GameId, cancellationToken);

		// 6. Invalidate cache
		await hybridCache.RemoveByTagAsync(nameof(Features.Games.ActiveGames), cancellationToken);

		return new DeleteGameSuccessful
		{
			GameId = command.GameId
		};
	}

	private bool IsAuthorized(string? creatorId)
	{
		if (string.IsNullOrEmpty(currentUserService.UserId))
		{
			return false;
		}

		return string.Equals(creatorId, currentUserService.UserId, StringComparison.OrdinalIgnoreCase);
	}
}

