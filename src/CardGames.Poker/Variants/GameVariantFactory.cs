#nullable enable
using System;
using System.Collections.Generic;

namespace CardGames.Poker.Variants;

/// <summary>
/// Default implementation of IGameVariantFactory and IGameVariantProvider.
/// Uses a registry pattern to allow dynamic variant registration via dependency injection.
/// </summary>
public class GameVariantFactory : IGameVariantFactory, IGameVariantProvider
{
    private readonly GameVariantRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the GameVariantFactory with the provided registry.
    /// </summary>
    /// <param name="registry">The variant registry containing registered variants.</param>
    public GameVariantFactory(GameVariantRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public object CreateGame(
        string variantId,
        IEnumerable<(string name, int chips)> players,
        int smallBlind,
        int bigBlind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variantId);
        ArgumentNullException.ThrowIfNull(players);

        var factory = _registry.GetFactory(variantId);
        if (factory == null)
        {
            throw new ArgumentException($"Variant '{variantId}' is not registered.", nameof(variantId));
        }

        return factory(players, smallBlind, bigBlind);
    }

    /// <inheritdoc/>
    public bool IsVariantRegistered(string variantId)
    {
        return !string.IsNullOrWhiteSpace(variantId) && _registry.IsRegistered(variantId);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<GameVariantInfo> GetAllVariants()
    {
        return _registry.GetAllVariantInfo();
    }

    /// <inheritdoc/>
    public GameVariantInfo? GetVariant(string variantId)
    {
        if (string.IsNullOrWhiteSpace(variantId))
        {
            return null;
        }

        return _registry.GetVariantInfo(variantId);
    }
}
