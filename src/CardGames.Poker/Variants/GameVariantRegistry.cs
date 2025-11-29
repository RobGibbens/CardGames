#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Variants;

/// <summary>
/// Delegate type for game creation functions used by the variant factory.
/// </summary>
/// <param name="players">Collection of player tuples containing name and starting chip stack.</param>
/// <param name="smallBlind">The small blind amount.</param>
/// <param name="bigBlind">The big blind amount.</param>
/// <returns>A game instance.</returns>
public delegate object GameCreationDelegate(
    IEnumerable<(string name, int chips)> players,
    int smallBlind,
    int bigBlind);

/// <summary>
/// Registration data for a game variant.
/// </summary>
internal record VariantRegistration(
    GameVariantInfo Info,
    GameCreationDelegate Factory);

/// <summary>
/// Registry for game variants. Stores variant metadata and factory functions.
/// Used internally by the GameVariantFactory.
/// </summary>
public class GameVariantRegistry
{
    private readonly Dictionary<string, VariantRegistration> _variants = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a new game variant with the registry.
    /// </summary>
    /// <param name="info">The variant metadata.</param>
    /// <param name="factory">The factory delegate to create game instances.</param>
    /// <exception cref="ArgumentNullException">Thrown when info or factory is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a variant with the same ID is already registered.</exception>
    public void RegisterVariant(GameVariantInfo info, GameCreationDelegate factory)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(factory);

        if (_variants.ContainsKey(info.Id))
        {
            throw new InvalidOperationException($"Variant '{info.Id}' is already registered.");
        }

        _variants[info.Id] = new VariantRegistration(info, factory);
    }

    /// <summary>
    /// Gets all registered variant metadata.
    /// </summary>
    internal IReadOnlyCollection<GameVariantInfo> GetAllVariantInfo()
    {
        return _variants.Values.Select(v => v.Info).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets metadata for a specific variant.
    /// </summary>
    internal GameVariantInfo? GetVariantInfo(string variantId)
    {
        return _variants.TryGetValue(variantId, out var registration) ? registration.Info : null;
    }

    /// <summary>
    /// Gets the factory delegate for a specific variant.
    /// </summary>
    internal GameCreationDelegate? GetFactory(string variantId)
    {
        return _variants.TryGetValue(variantId, out var registration) ? registration.Factory : null;
    }

    /// <summary>
    /// Checks if a variant is registered.
    /// </summary>
    internal bool IsRegistered(string variantId)
    {
        return _variants.ContainsKey(variantId);
    }
}
