using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

/// <summary>
/// MediatR pipeline behavior that broadcasts lobby updates via SignalR
/// after successful execution of lobby state-changing commands.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LobbyStateBroadcastingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILobbyBroadcaster _broadcaster;
    private readonly CardsDbContext _dbContext;
    private readonly ILogger<LobbyStateBroadcastingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyStateBroadcastingBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    public LobbyStateBroadcastingBehavior(
        ILobbyBroadcaster broadcaster,
        CardsDbContext dbContext,
        ILogger<LobbyStateBroadcastingBehavior<TRequest, TResponse>> logger)
    {
        _broadcaster = broadcaster;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Execute the handler first
        var response = await next();

        // Only broadcast for lobby state-changing commands
        if (request is not ILobbyStateChangingCommand lobbyCommand)
        {
            return response;
        }

        // Only broadcast if the command was successful
        if (!IsSuccessfulResponse(response))
        {
            _logger.LogDebug(
                "Skipping lobby broadcast for {CommandType} - command did not succeed",
                typeof(TRequest).Name);
            return response;
        }

        try
        {
            _logger.LogDebug(
                "Broadcasting lobby update for game {GameId} after {CommandType}",
                lobbyCommand.GameId, typeof(TRequest).Name);

            var gameCreatedDto = await BuildGameCreatedDtoAsync(lobbyCommand.GameId, cancellationToken);
            if (gameCreatedDto is not null)
            {
                await _broadcaster.BroadcastGameCreatedAsync(gameCreatedDto, cancellationToken);

                _logger.LogInformation(
                    "Successfully broadcast lobby update for game {GameId} after {CommandType}",
                    lobbyCommand.GameId, typeof(TRequest).Name);
            }
            else
            {
                _logger.LogWarning(
                    "Could not build GameCreatedDto for game {GameId} - game not found",
                    lobbyCommand.GameId);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the command - the state change was successful
            _logger.LogError(ex,
                "Failed to broadcast lobby update for game {GameId} after {CommandType}",
                lobbyCommand.GameId, typeof(TRequest).Name);
        }

        return response;
    }

    private async Task<GameCreatedDto?> BuildGameCreatedDtoAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await _dbContext.Games
            .AsNoTracking()
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

            if (game is null)
            {
                return null;
            }

            var gameTypeCode = game.GameType?.Code ?? "Unknown";

            // Look up metadata from registry for image and description
            string? metadataName = null;
            string? description = game.GameType?.Description;
            string? imageName = null;

            if (PokerGameMetadataRegistry.TryGet(gameTypeCode, out var metadata) && metadata is not null)
            {
                metadataName = metadata.Name;
                description = metadata.Description;
                imageName = metadata.ImageName;
            }

            return new GameCreatedDto
            {
                GameId = game.Id,
                Name = game.Name,
                GameTypeId = game.GameTypeId,
                GameTypeCode = gameTypeCode,
                GameTypeName = game.GameType?.Name ?? "Unknown",
                GameTypeMetadataName = metadataName,
                GameTypeDescription = description,
                GameTypeImageName = imageName,
                CurrentPhase = game.CurrentPhase,
                Status = game.Status.ToString(),
                CreatedAt = game.CreatedAt,
                Ante = game.Ante ?? 0,
                MinBet = game.MinBet ?? 0,
                CreatedById = game.CreatedById,
                CreatedByName = game.CreatedByName
            };
        }

    /// <summary>
    /// Determines if the response indicates a successful command execution.
    /// Handles OneOf discriminated unions used by command handlers.
    /// </summary>
    private static bool IsSuccessfulResponse(TResponse response)
    {
        if (response is null)
        {
            return false;
        }

        // Handle OneOf<TSuccess, TError> pattern used by command handlers
        // The first type parameter is typically the success type
        var responseType = response.GetType();

        // Check if it's a OneOf type
        if (responseType.IsGenericType)
        {
            var genericTypeDef = responseType.GetGenericTypeDefinition();
            if (genericTypeDef.FullName?.StartsWith("OneOf.OneOf") == true)
            {
                // Use reflection to check which variant is active
                // OneOf has an Index property: 0 = first type (success), 1+ = error types
                var indexProperty = responseType.GetProperty("Index");
                if (indexProperty is not null)
                {
                    var index = (int)indexProperty.GetValue(response)!;
                    return index == 0; // First type is typically the success type
                }
            }
        }

        // For non-OneOf responses, assume success if we got here
        return true;
    }
}
