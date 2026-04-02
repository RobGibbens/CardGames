using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Singleton coordinator that provides per-game single-writer access via <see cref="SemaphoreSlim"/>.
/// Ensures no two concurrent requests mutate the same game's runtime state simultaneously.
/// </summary>
public sealed class GameExecutionCoordinator : IGameExecutionCoordinator, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly IGameStateManager _gameStateManager;
    private readonly ILogger<GameExecutionCoordinator> _logger;

    public GameExecutionCoordinator(
        IGameStateManager gameStateManager,
        ILogger<GameExecutionCoordinator> logger)
    {
        _gameStateManager = gameStateManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T> ExecuteAsync<T>(
        Guid gameId,
        Func<ActiveGameRuntimeState, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var state = await _gameStateManager.GetOrLoadGameAsync(gameId, cancellationToken);
            if (state is null)
            {
                throw new InvalidOperationException($"Game {gameId} not found in memory or database.");
            }

            var result = await action(state, cancellationToken);
            state.IsDirty = true;
            state.Version++;
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Guid gameId,
        Func<ActiveGameRuntimeState, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync<object?>(gameId, async (state, ct) =>
        {
            await action(state, ct);
            return null;
        }, cancellationToken);
    }

    /// <summary>
    /// Cleans up semaphores for removed games to prevent memory leaks.
    /// Called by the eviction service.
    /// </summary>
    public void CleanupLock(Guid gameId)
    {
        if (_locks.TryRemove(gameId, out var semaphore))
        {
            semaphore.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _locks)
        {
            kvp.Value.Dispose();
        }

        _locks.Clear();
    }
}
