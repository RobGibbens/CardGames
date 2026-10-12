using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using MediatR;

namespace CardGames.Poker.Api.Infrastructure.PipelineBehaviors;

/// <summary>
///     MediatR pipeline behavior that synchronizes league event completion
///     when a linked game completes. This behavior runs after all other behaviors
///     to ensure game state is fully persisted before league sync is triggered.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LeagueGameCompletionSyncBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : IRequest<TResponse>
{
	private readonly ILogger<LeagueGameCompletionSyncBehavior<TRequest, TResponse>> _logger;
	private readonly IServiceScopeFactory _scopeFactory;

	public LeagueGameCompletionSyncBehavior(
		IServiceScopeFactory scopeFactory,
		ILogger<LeagueGameCompletionSyncBehavior<TRequest, TResponse>> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken)
	{
		// Execute the handler and all prior behaviors
		var response = await next(cancellationToken);

		// Only process for game state-changing commands
		if (request is not IGameStateChangingCommand gameCommand) return response;

		var gameId = gameCommand.GameId;

		// The request scope owns the DbContext that just saved this command and is about to
		// broadcast that save. League sync has to use its own scope so the two do not query
		// that context at the same time.
		_ = Task.Run(
			async () =>
			{
				try
				{
					await using var scope = _scopeFactory.CreateAsyncScope();
					var leagueCompletionSync =
						scope.ServiceProvider.GetRequiredService<LeagueGameCompletionSyncService>();
					await leagueCompletionSync.SyncLeagueEventCompletionAsync(gameId, CancellationToken.None);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex,
						"Error in league completion sync for game {GameId} after {CommandType}",
						gameId, typeof(TRequest).Name);
				}
			},
			CancellationToken.None);

		return response;
	}
}
