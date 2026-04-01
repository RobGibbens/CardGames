namespace CardGames.Poker.Api.Infrastructure.Caching;

/// <summary>
/// Single source of truth for HybridCache keys and tags used by game-related queries and invalidation.
/// </summary>
public static class GameCacheKeys
{
	public const string ActiveGamesTag = "active-games";
	public const string AvailableGamesTag = "available-games";

	public static string GameTag(Guid gameId) => $"game:{gameId}";
	public static string GamePlayersTag(Guid gameId) => $"game-players:{gameId}";
	public static string BettingRoundTag(Guid gameId) => $"betting-round:{gameId}";
	public static string DrawPlayerTag(Guid gameId) => $"draw-player:{gameId}";
	public static string CurrentPlayerTurnTag(Guid gameId) => $"current-player-turn:{gameId}";

	/// <summary>
	/// Returns all per-game tags that should be invalidated after any gameplay mutation.
	/// </summary>
	public static string[] BuildPerGameTags(Guid gameId) =>
	[
		GameTag(gameId),
		GamePlayersTag(gameId),
		BettingRoundTag(gameId),
		DrawPlayerTag(gameId),
		CurrentPlayerTurnTag(gameId)
	];

	/// <summary>
	/// Returns all tags (per-game + global) that should be invalidated after any gameplay mutation.
	/// </summary>
	public static string[] BuildAllMutationTags(Guid gameId) =>
	[
		.. BuildPerGameTags(gameId),
		ActiveGamesTag
	];
}
