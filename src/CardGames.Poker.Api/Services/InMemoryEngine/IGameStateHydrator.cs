namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Loads a game's full state from the database and produces a detached
/// <see cref="ActiveGameRuntimeState"/> suitable for in-memory mutation.
/// </summary>
public interface IGameStateHydrator
{
    /// <summary>
    /// Loads the game and all related entities from the database, maps them
    /// to runtime model types, and returns the detached state.
    /// Returns <c>null</c> if the game does not exist.
    /// </summary>
    Task<ActiveGameRuntimeState?> HydrateFromDatabaseAsync(Guid gameId, CancellationToken cancellationToken);
}
