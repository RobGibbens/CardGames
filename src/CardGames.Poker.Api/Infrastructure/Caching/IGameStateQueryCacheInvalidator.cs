namespace CardGames.Poker.Api.Infrastructure.Caching;

/// <summary>
/// Invalidates HybridCache tags for game-related queries after state mutations.
/// </summary>
public interface IGameStateQueryCacheInvalidator
{
	/// <summary>
	/// Invalidates all per-game and global tags after a gameplay mutation.
	/// </summary>
	ValueTask InvalidateAfterMutationAsync(Guid gameId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates only per-game tags (not the global active-games list).
	/// </summary>
	ValueTask InvalidateGameAsync(Guid gameId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Invalidates only the global active-games list tag.
	/// </summary>
	ValueTask InvalidateActiveGamesAsync(CancellationToken cancellationToken = default);
}
