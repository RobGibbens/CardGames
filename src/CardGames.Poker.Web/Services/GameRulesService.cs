using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Client-side service that fetches and caches game rules.
/// Provides game metadata to the UI for dynamic rendering.
/// </summary>
public sealed class GameRulesService
{
    private readonly IGamesApi _gamesApi;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameRulesService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameRulesService"/> class.
    /// </summary>
    public GameRulesService(
        IGamesApi gamesApi,
        IMemoryCache cache,
        ILogger<GameRulesService> logger)
    {
        _gamesApi = gamesApi;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets the game rules for the specified game type code.
    /// Results are cached for 1 hour to minimize API calls.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
    /// <returns>The game rules response, or null if not found.</returns>
    public async Task<GetGameRulesResponse?> GetGameRulesAsync(string gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return null;
        }

        var cacheKey = $"GameRules:{gameTypeCode.ToUpperInvariant()}";

        if (_cache.TryGetValue(cacheKey, out GetGameRulesResponse? cached))
        {
            return cached;
        }

        try
        {
            var response = await _gamesApi.GamesGetGameRulesAsync(gameTypeCode);

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                _cache.Set(cacheKey, response.Content, cacheOptions);
                _logger.LogDebug("Cached game rules for {GameTypeCode}", gameTypeCode);

                return response.Content;
            }

            _logger.LogWarning(
                "Failed to fetch game rules for {GameTypeCode}: {StatusCode}",
                gameTypeCode,
                response.StatusCode);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching game rules for {GameTypeCode}", gameTypeCode);
            return null;
        }
    }

    /// <summary>
    /// Invalidates the cached game rules for the specified game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code to invalidate.</param>
    public void InvalidateCache(string gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return;
        }

        var cacheKey = $"GameRules:{gameTypeCode.ToUpperInvariant()}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for {GameTypeCode}", gameTypeCode);
    }
}
