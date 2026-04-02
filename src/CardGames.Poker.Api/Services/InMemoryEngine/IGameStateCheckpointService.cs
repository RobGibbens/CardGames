namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Writes the current in-memory game state back to the database as a checkpoint.
/// </summary>
public interface IGameStateCheckpointService
{
    /// <summary>
    /// Persists the full game state to the database, updating existing rows
    /// and inserting new ones. On success, clears <see cref="ActiveGameRuntimeState.IsDirty"/>
    /// and captures the new <c>RowVersion</c> values.
    /// </summary>
    Task CheckpointAsync(ActiveGameRuntimeState state, CancellationToken cancellationToken);
}
