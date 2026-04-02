using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Singleton managing all active game runtime states in a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class GameStateManager : IGameStateManager
{
    private readonly ConcurrentDictionary<Guid, ActiveGameRuntimeState> _games = new();
    private readonly IGameStateHydrator _hydrator;
    private readonly IOptions<InMemoryEngineOptions> _options;
    private readonly ILogger<GameStateManager> _logger;

    private readonly Counter<long> _loadCounter;
    private readonly Counter<long> _evictCounter;

    public GameStateManager(
        IGameStateHydrator hydrator,
        IOptions<InMemoryEngineOptions> options,
        ILogger<GameStateManager> logger,
        IMeterFactory meterFactory)
    {
        _hydrator = hydrator;
        _options = options;
        _logger = logger;

        var meter = meterFactory.Create("CardGames.Poker.Api.GameStateManager");
        _loadCounter = meter.CreateCounter<long>("game_state_manager_load");
        _evictCounter = meter.CreateCounter<long>("game_state_manager_evict");
    }

    /// <inheritdoc />
    public bool TryGetGame(Guid gameId, out ActiveGameRuntimeState state)
    {
        return _games.TryGetValue(gameId, out state!);
    }

    /// <inheritdoc />
    public async Task<ActiveGameRuntimeState?> GetOrLoadGameAsync(Guid gameId, CancellationToken cancellationToken)
    {
        if (_games.TryGetValue(gameId, out var existing))
            return existing;

        var state = await _hydrator.HydrateFromDatabaseAsync(gameId, cancellationToken);
        if (state is null)
            return null;

        // Use GetOrAdd to handle concurrent LoadGameAsync calls for the same game.
        // If another thread loaded it first, use that one.
        var result = _games.GetOrAdd(gameId, state);
        if (ReferenceEquals(result, state))
        {
            _loadCounter.Add(1);
            _logger.LogInformation("Loaded game {GameId} into in-memory engine (hand {Hand}, phase {Phase})",
                gameId, state.CurrentHandNumber, state.CurrentPhase);
        }

        return result;
    }

    /// <inheritdoc />
    public void SetGame(ActiveGameRuntimeState state)
    {
        _games[state.GameId] = state;
    }

    /// <inheritdoc />
    public bool RemoveGame(Guid gameId)
    {
        if (_games.TryRemove(gameId, out _))
        {
            _evictCounter.Add(1);
            _logger.LogInformation("Removed game {GameId} from in-memory engine", gameId);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetActiveGameIds() => _games.Keys.ToArray();

    /// <inheritdoc />
    public int Count => _games.Count;
}
