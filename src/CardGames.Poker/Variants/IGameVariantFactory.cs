using System.Collections.Generic;

namespace CardGames.Poker.Variants;

/// <summary>
/// Factory interface for creating poker game instances from variant identifiers.
/// Implementations should be registered via dependency injection and support
/// variant registration via the Strategy pattern.
/// </summary>
public interface IGameVariantFactory
{
    /// <summary>
    /// Creates a game instance for the specified variant.
    /// </summary>
    /// <param name="variantId">The unique identifier of the variant (e.g., "texas-holdem", "omaha").</param>
    /// <param name="players">Collection of player tuples containing name and starting chip stack.</param>
    /// <param name="smallBlind">The small blind amount.</param>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <returns>A game instance configured for the specified variant.</returns>
    /// <exception cref="ArgumentException">Thrown when the variant is not registered.</exception>
    object CreateGame(
        string variantId,
        IEnumerable<(string name, int chips)> players,
        int smallBlind,
        int bigBlind);

    /// <summary>
    /// Checks if a variant is registered with the factory.
    /// </summary>
    /// <param name="variantId">The unique identifier of the variant.</param>
    /// <returns>True if the variant is registered, false otherwise.</returns>
    bool IsVariantRegistered(string variantId);
}
