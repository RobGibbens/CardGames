using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Handles the ToggleOddsVisibilityCommand.
/// </summary>
public sealed class ToggleOddsVisibilityCommandHandler(
    CardsDbContext context,
    ICurrentUserService currentUserService,
    IGameStateBroadcaster gameStateBroadcaster,
    ILogger<ToggleOddsVisibilityCommandHandler> logger)
    : IRequestHandler<ToggleOddsVisibilityCommand, OneOf<ToggleOddsVisibilitySuccessful, ToggleOddsVisibilityError>>
{
    /// <inheritdoc />
    public async Task<OneOf<ToggleOddsVisibilitySuccessful, ToggleOddsVisibilityError>> Handle(
        ToggleOddsVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        var game = await context.Games
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new ToggleOddsVisibilityError
            {
                Code = ToggleOddsVisibilityErrorCode.GameNotFound,
                Message = $"Game with ID {command.GameId} not found."
            };
        }

        if (!IsAuthorized(game))
        {
            logger.LogWarning(
                "User {UserId} attempted to toggle odds visibility for game {GameId} but is not authorized",
                currentUserService.UserId,
                command.GameId);

            return new ToggleOddsVisibilityError
            {
                Code = ToggleOddsVisibilityErrorCode.NotAuthorized,
                Message = "You are not authorized to edit this table's settings."
            };
        }

        if (game.Status is GameStatus.Completed or GameStatus.Cancelled)
        {
            return new ToggleOddsVisibilityError
            {
                Code = ToggleOddsVisibilityErrorCode.GameEnded,
                Message = "This game has ended and odds visibility can no longer be changed."
            };
        }

        var now = DateTimeOffset.UtcNow;
        game.AreOddsVisibleToAllPlayers = command.AreOddsVisibleToAllPlayers;
        game.UpdatedAt = now;
        game.UpdatedById = currentUserService.UserId;
        game.UpdatedByName = currentUserService.UserName;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} set AreOddsVisibleToAllPlayers={AreOddsVisible} for game {GameId}",
            currentUserService.UserId,
            command.AreOddsVisibleToAllPlayers,
            game.Id);

        try
        {
            var notification = new OddsVisibilityUpdatedDto
            {
                GameId = game.Id,
                AreOddsVisibleToAllPlayers = game.AreOddsVisibleToAllPlayers,
                UpdatedAt = now,
                UpdatedById = currentUserService.UserId,
                UpdatedByName = currentUserService.UserName
            };
            await gameStateBroadcaster.BroadcastOddsVisibilityUpdatedAsync(notification, cancellationToken);

            var seatedPlayerCount = game.GamePlayers.Count(p => p.Status == GamePlayerStatus.Active);
            var settingsDto = GetTableSettingsMapper.MapToDto(game, seatedPlayerCount);
            var settingsNotification = new TableSettingsUpdatedDto
            {
                GameId = game.Id,
                UpdatedAt = now,
                UpdatedById = currentUserService.UserId,
                UpdatedByName = currentUserService.UserName,
                Settings = settingsDto
            };
            await gameStateBroadcaster.BroadcastTableSettingsUpdatedAsync(settingsNotification, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast odds visibility update for game {GameId}", game.Id);
        }

        return new ToggleOddsVisibilitySuccessful
        {
            GameId = game.Id,
            AreOddsVisibleToAllPlayers = game.AreOddsVisibleToAllPlayers
        };
    }

    private bool IsAuthorized(Game game)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(game.CreatedById) &&
            string.Equals(game.CreatedById, currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(game.CreatedByName))
        {
            var userName = currentUserService.UserName;
            var userEmail = currentUserService.UserEmail;

            if (string.Equals(game.CreatedByName, userName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.CreatedByName, userEmail, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
