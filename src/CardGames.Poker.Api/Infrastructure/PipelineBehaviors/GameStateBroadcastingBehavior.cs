using CardGames.Poker.Api.Services;
using MediatR;
using OneOf;

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
    private readonly ILogger<GameStateBroadcastingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateBroadcastingBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    public GameStateBroadcastingBehavior(
        IGameStateBroadcaster broadcaster,
        ILogger<GameStateBroadcastingBehavior<TRequest, TResponse>> logger)
    {
        _broadcaster = broadcaster;
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

        try
        {
            _logger.LogDebug(
                "Broadcasting game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);

            await _broadcaster.BroadcastGameStateAsync(gameCommand.GameId, cancellationToken);

            _logger.LogInformation(
                "Successfully broadcast game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            // Log but don't fail the command - the state change was successful
            _logger.LogError(ex,
                "Failed to broadcast game state for game {GameId} after {CommandType}",
                gameCommand.GameId, typeof(TRequest).Name);
        }

        return response;
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
