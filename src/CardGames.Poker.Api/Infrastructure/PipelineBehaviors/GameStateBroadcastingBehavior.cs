using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Caching;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

/// <summary>
/// MediatR pipeline behavior that broadcasts game state via SignalR
/// after successful execution of game state-changing commands.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class GameStateBroadcastingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IGameStateBroadcaster _broadcaster;
    private readonly IGameStateQueryCacheInvalidator _gameStateQueryCacheInvalidator;
    private readonly ILogger<GameStateBroadcastingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateBroadcastingBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    public GameStateBroadcastingBehavior(
        IGameStateBroadcaster broadcaster,
        IGameStateQueryCacheInvalidator gameStateQueryCacheInvalidator,
        ILogger<GameStateBroadcastingBehavior<TRequest, TResponse>> logger)
    {
        _broadcaster = broadcaster;
        _gameStateQueryCacheInvalidator = gameStateQueryCacheInvalidator;
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

        // Only broadcast for game state-changing commands
        if (request is not IGameStateChangingCommand gameCommand)
        {
            return response;
        }

        // Only broadcast if the command was successful
        if (!IsSuccessfulResponse(response))
        {
            _logger.LogDebug(
                "Skipping broadcast for {CommandType} - command did not succeed",
                typeof(TRequest).Name);
            return response;
        }

        if (!ShouldBroadcastGameState(response))
        {
            _logger.LogDebug(
                "Skipping automatic game-state broadcast for {CommandType} because the response indicates no state mutation",
                typeof(TRequest).Name);
            return response;
        }

        try
        {
            _logger.LogDebug(
                "Broadcasting game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);

            // Invalidate query cache before broadcasting so the broadcaster reads fresh state
            await _gameStateQueryCacheInvalidator.InvalidateAfterMutationAsync(gameCommand.GameId, cancellationToken);

            var actionResult = ExtractPlayerActionResult(response);
            if (actionResult is not null)
            {
                _logger.LogDebug(
                    "Broadcasting player action for game {GameId}, seat {SeatIndex}: {Action}",
                    actionResult.GameId, actionResult.PlayerSeatIndex, actionResult.ActionDescription);

                await _broadcaster.BroadcastPlayerActionAsync(
                    actionResult.GameId,
                    actionResult.PlayerSeatIndex,
                    actionResult.PlayerName,
                    actionResult.ActionDescription,
                    cancellationToken);
            }

            await _broadcaster.BroadcastGameStateAsync(gameCommand.GameId, cancellationToken);

            _logger.LogInformation(
                "Successfully broadcast game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to broadcast game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);
        }

        return response;
    }

    /// <summary>
    /// Extracts the player action result from the response if it implements IPlayerActionResult.
    /// Handles OneOf discriminated unions by checking the success value.
    /// </summary>
    private static IPlayerActionResult? ExtractPlayerActionResult(TResponse response)
    {
        return ExtractResponseValue<IPlayerActionResult>(response);
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

    private static bool ShouldBroadcastGameState(TResponse response)
    {
        var broadcastResult = ExtractResponseValue<IGameStateBroadcastResult>(response);
        return broadcastResult?.ShouldBroadcastGameState ?? true;
    }

    private static TInterface? ExtractResponseValue<TInterface>(TResponse response)
        where TInterface : class
    {
        if (response is null)
        {
            return null;
        }

        if (response is TInterface directValue)
        {
            return directValue;
        }

        var responseType = response.GetType();
        if (!responseType.IsGenericType)
        {
            return null;
        }

        var genericTypeDef = responseType.GetGenericTypeDefinition();
        if (genericTypeDef.FullName?.StartsWith("OneOf.OneOf") != true)
        {
            return null;
        }

        var valueProperty = responseType.GetProperty("Value");
        if (valueProperty?.GetValue(response) is TInterface value)
        {
            return value;
        }

        return null;
    }
}
