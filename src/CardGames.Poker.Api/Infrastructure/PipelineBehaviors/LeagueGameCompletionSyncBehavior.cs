using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.Infrastructure;
using MediatR;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

/// <summary>
/// MediatR pipeline behavior that synchronizes league event completion
/// when a linked game completes. This behavior runs after all other behaviors
/// to ensure game state is fully persisted before league sync is triggered.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LeagueGameCompletionSyncBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly LeagueGameCompletionSyncService _leagueCompletionSync;
    private readonly ILogger<LeagueGameCompletionSyncBehavior<TRequest, TResponse>> _logger;

    public LeagueGameCompletionSyncBehavior(
        LeagueGameCompletionSyncService leagueCompletionSync,
        ILogger<LeagueGameCompletionSyncBehavior<TRequest, TResponse>> logger)
    {
        _leagueCompletionSync = leagueCompletionSync;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Execute the handler and all prior behaviors
        var response = await next();

        // Only process for game state-changing commands
        if (request is not IGameStateChangingCommand gameCommand)
        {
            return response;
        }

        // Don't block the response on league sync - fire and forget with error logging
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await _leagueCompletionSync.SyncLeagueEventCompletionAsync(
                        gameCommand.GameId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error in league completion sync for game {GameId} after {CommandType}",
                        gameCommand.GameId, typeof(TRequest).Name);
                }
            },
            cancellationToken);

        return response;
    }
}
