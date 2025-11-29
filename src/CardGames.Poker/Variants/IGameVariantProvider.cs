#nullable enable
using System.Collections.Generic;

namespace CardGames.Poker.Variants;

/// <summary>
/// Represents metadata about a registered game variant.
/// </summary>
public record GameVariantInfo(
    /// <summary>
    /// Unique identifier for the variant.
    /// </summary>
    string Id,

    /// <summary>
    /// Display name of the variant.
    /// </summary>
    string Name,

    /// <summary>
    /// Optional description of the variant.
    /// </summary>
    string? Description = null,

    /// <summary>
    /// Minimum number of players supported.
    /// </summary>
    int MinPlayers = 2,

    /// <summary>
    /// Maximum number of players supported.
    /// </summary>
    int MaxPlayers = 10);

/// <summary>
/// Provider interface for retrieving information about registered game variants.
/// </summary>
public interface IGameVariantProvider
{
    /// <summary>
    /// Gets all registered game variants.
    /// </summary>
    /// <returns>A collection of all registered variant metadata.</returns>
    IReadOnlyCollection<GameVariantInfo> GetAllVariants();

    /// <summary>
    /// Gets metadata for a specific variant.
    /// </summary>
    /// <param name="variantId">The unique identifier of the variant.</param>
    /// <returns>The variant metadata, or null if not found.</returns>
    GameVariantInfo? GetVariant(string variantId);
}
