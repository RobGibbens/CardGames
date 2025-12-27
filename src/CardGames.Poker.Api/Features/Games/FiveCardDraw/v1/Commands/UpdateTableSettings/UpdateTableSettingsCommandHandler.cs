using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.UpdateTableSettings;

/// <summary>
/// Handles the UpdateTableSettingsCommand.
/// </summary>
public sealed class UpdateTableSettingsCommandHandler(
    CardsDbContext context,
    ICurrentUserService currentUserService,
    IGameStateBroadcaster gameStateBroadcaster,
    ILogger<UpdateTableSettingsCommandHandler> logger)
    : IRequestHandler<UpdateTableSettingsCommand, OneOf<UpdateTableSettingsSuccessful, UpdateTableSettingsError>>
{
    /// <inheritdoc />
    public async Task<OneOf<UpdateTableSettingsSuccessful, UpdateTableSettingsError>> Handle(
        UpdateTableSettingsCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load the game with related data
        var game = await context.Games
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.GameNotFound,
                Message = $"Game with ID {command.GameId} not found."
            };
        }

        // 2. Verify authorization - only the creator can edit
        if (!IsAuthorized(game))
        {
            logger.LogWarning(
                "User {UserId} attempted to edit table settings for game {GameId} but is not authorized",
                currentUserService.UserId,
                command.GameId);

            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.NotAuthorized,
                Message = "You are not authorized to edit this table's settings."
            };
        }

        // 3. Verify the game is in an editable phase
        if (!GetTableSettingsMapper.IsPhaseEditable(game.CurrentPhase))
        {
            logger.LogWarning(
                "User {UserId} attempted to edit table settings for game {GameId} in phase {Phase}",
                currentUserService.UserId,
                command.GameId,
                game.CurrentPhase);

            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.PhaseNotEditable,
                Message = $"Table settings cannot be edited while the game is in '{game.CurrentPhase}' phase.",
                CurrentPhase = game.CurrentPhase
            };
        }

        // 4. Verify concurrency token
        var expectedRowVersion = Convert.FromBase64String(command.RowVersion);
        if (!game.RowVersion.SequenceEqual(expectedRowVersion))
        {
            logger.LogWarning(
                "Concurrency conflict when updating table settings for game {GameId}",
                command.GameId);

            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.ConcurrencyConflict,
                Message = "The table settings have been modified by another user. Please refresh and try again."
            };
        }

        // 5. Validate settings
        var validationError = ValidateSettings(command, game);
        if (validationError is not null)
        {
            return validationError;
        }

        // 6. Apply updates
        var now = DateTimeOffset.UtcNow;
        ApplyUpdates(game, command, now);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concurrency conflict when saving table settings for game {GameId}", command.GameId);

            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.ConcurrencyConflict,
                Message = "The table settings have been modified by another user. Please refresh and try again."
            };
        }

                logger.LogInformation(
                    "User {UserId} updated table settings for game {GameId}",
                    currentUserService.UserId,
                    command.GameId);

                // 7. Build response
                var seatedPlayerCount = game.GamePlayers.Count(p => p.Status == GamePlayerStatus.Active);
                var response = UpdateTableSettingsMapper.MapToResponse(game, seatedPlayerCount);

                // 8. Broadcast the settings update to all connected clients
                try
                {
                    var settingsDto = GetTableSettingsMapper.MapToDto(game, seatedPlayerCount);
                    var notification = new TableSettingsUpdatedDto
                    {
                        GameId = game.Id,
                        UpdatedById = currentUserService.UserId,
                        UpdatedByName = currentUserService.UserName,
                        UpdatedAt = now,
                        Settings = settingsDto
                    };
                    await gameStateBroadcaster.BroadcastTableSettingsUpdatedAsync(notification, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log but don't fail - the update was successful
                    logger.LogWarning(ex, "Failed to broadcast TableSettingsUpdated for game {GameId}", game.Id);
                }

                return new UpdateTableSettingsSuccessful
                {
                    GameId = game.Id,
                    Settings = response
                };
            }

    /// <summary>
    /// Checks if the current user is authorized to edit the table settings.
    /// Currently, only the table creator can edit.
    /// </summary>
    private bool IsAuthorized(Game game)
    {
        if (!currentUserService.IsAuthenticated)
        {
            return false;
        }

        // Check by user ID first
        if (!string.IsNullOrEmpty(game.CreatedById) &&
            string.Equals(game.CreatedById, currentUserService.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fall back to checking by name/email for legacy data
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

    /// <summary>
    /// Validates the settings in the command.
    /// </summary>
    private static UpdateTableSettingsError? ValidateSettings(UpdateTableSettingsCommand command, Game game)
    {
        // Validate BigBlind >= SmallBlind if both are provided
        if (command is { SmallBlind: not null, BigBlind: not null })
        {
            if (command.BigBlind < command.SmallBlind)
            {
                return new UpdateTableSettingsError
                {
                    Code = UpdateTableSettingsErrorCode.ValidationFailed,
                    Message = "Big blind must be greater than or equal to small blind."
                };
            }
        }

        // Validate Ante is non-negative
        if (command.Ante.HasValue && command.Ante < 0)
        {
            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.ValidationFailed,
                Message = "Ante must be zero or greater."
            };
        }

        // Validate MinBet is positive
        if (command.MinBet.HasValue && command.MinBet <= 0)
        {
            return new UpdateTableSettingsError
            {
                Code = UpdateTableSettingsErrorCode.ValidationFailed,
                Message = "Minimum bet must be greater than zero."
            };
        }

        return null;
    }

    /// <summary>
    /// Applies the updates from the command to the game entity.
    /// </summary>
    private void ApplyUpdates(Game game, UpdateTableSettingsCommand command, DateTimeOffset now)
    {
        if (command.Name is not null)
        {
            game.Name = command.Name;
        }

        if (command.Ante.HasValue)
        {
            game.Ante = command.Ante.Value;
        }

        if (command.MinBet.HasValue)
        {
            game.MinBet = command.MinBet.Value;
        }

        if (command.SmallBlind.HasValue)
        {
            game.SmallBlind = command.SmallBlind.Value;
        }

        if (command.BigBlind.HasValue)
        {
            game.BigBlind = command.BigBlind.Value;
        }

        game.UpdatedAt = now;
        game.UpdatedById = currentUserService.UserId;
        game.UpdatedByName = currentUserService.UserName;
    }
}
