namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Serializes access to a game's runtime state via per-game locks.
/// All game-state-mutating command handlers must call <see cref="ExecuteAsync{T}"/>
/// to ensure single-writer semantics per game.
/// </summary>
public interface IGameExecutionCoordinator
{
    /// <summary>
    /// Acquires the per-game lock, loads the game into <see cref="IGameStateManager"/> if necessary,
    /// and executes <paramref name="action"/> with exclusive access to the game state.
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Guid gameId,
        Func<ActiveGameRuntimeState, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);

    /// <summary>
    /// Non-generic overload for commands that don't return a value.
    /// </summary>
    Task ExecuteAsync(
        Guid gameId,
        Func<ActiveGameRuntimeState, CancellationToken, Task> action,
        CancellationToken cancellationToken);
}
