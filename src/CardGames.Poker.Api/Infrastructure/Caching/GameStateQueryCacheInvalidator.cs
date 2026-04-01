using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Infrastructure.Caching;

/// <summary>
/// Invalidates HybridCache tags for game-related queries after state mutations.
/// </summary>
public sealed class GameStateQueryCacheInvalidator(
	HybridCache hybridCache,
	ILogger<GameStateQueryCacheInvalidator> logger)
	: IGameStateQueryCacheInvalidator
{
	public async ValueTask InvalidateAfterMutationAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		var tags = GameCacheKeys.BuildAllMutationTags(gameId);
		logger.LogDebug("Invalidating {TagCount} cache tags for game {GameId}", tags.Length, gameId);

		foreach (var tag in tags)
		{
			await hybridCache.RemoveByTagAsync(tag, cancellationToken);
		}
	}

	public async ValueTask InvalidateGameAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		var tags = GameCacheKeys.BuildPerGameTags(gameId);
		logger.LogDebug("Invalidating {TagCount} per-game cache tags for game {GameId}", tags.Length, gameId);

		foreach (var tag in tags)
		{
			await hybridCache.RemoveByTagAsync(tag, cancellationToken);
		}
	}

	public async ValueTask InvalidateActiveGamesAsync(CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Invalidating active-games cache tag");
		await hybridCache.RemoveByTagAsync(GameCacheKeys.ActiveGamesTag, cancellationToken);
	}
}
