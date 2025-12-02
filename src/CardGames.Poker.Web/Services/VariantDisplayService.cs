using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Service for providing variant-specific display configuration to UI components.
/// </summary>
public class VariantDisplayService
{
    private readonly Dictionary<PokerVariant, VariantDisplayConfigDto> _configCache = new();

    /// <summary>
    /// Gets the display configuration for a specific poker variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The display configuration, or null if the variant is not supported.</returns>
    public VariantDisplayConfigDto? GetDisplayConfig(PokerVariant variant)
    {
        if (_configCache.TryGetValue(variant, out var cached))
        {
            return cached;
        }

        var loader = VariantConfigLoader.ForVariant(variant);
        if (loader == null)
        {
            return null;
        }

        var config = loader.ToDisplayConfig();
        _configCache[variant] = config;
        return config;
    }

    /// <summary>
    /// Gets all supported variant display configurations.
    /// </summary>
    /// <returns>A collection of all supported variant configurations.</returns>
    public IReadOnlyCollection<VariantDisplayConfigDto> GetAllDisplayConfigs()
    {
        var configs = new List<VariantDisplayConfigDto>();

        foreach (var variant in Enum.GetValues<PokerVariant>())
        {
            var config = GetDisplayConfig(variant);
            if (config != null)
            {
                configs.Add(config);
            }
        }

        return configs.AsReadOnly();
    }

    /// <summary>
    /// Gets the number of hole cards to display for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The number of hole cards, or 2 as default.</returns>
    public int GetHoleCardCount(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.HoleCardCount ?? 2;
    }

    /// <summary>
    /// Gets the number of community cards for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The number of community cards, or 0 if none.</returns>
    public int GetCommunityCardCount(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.CommunityCardCount ?? 0;
    }

    /// <summary>
    /// Gets whether to show community cards for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>True if community cards should be shown.</returns>
    public bool ShowCommunityCards(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.HasCommunityCards ?? false;
    }

    /// <summary>
    /// Gets whether the variant is a stud game with face-up/down cards.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>True if it's a stud game.</returns>
    public bool IsStudGame(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.IsStudGame ?? false;
    }

    /// <summary>
    /// Gets the face-up card indices for a stud game.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The indices of face-up cards.</returns>
    public IReadOnlyList<int> GetFaceUpCardIndices(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.FaceUpCardIndices ?? Array.Empty<int>();
    }

    /// <summary>
    /// Gets the face-down card indices for a stud game.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The indices of face-down cards.</returns>
    public IReadOnlyList<int> GetFaceDownCardIndices(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.FaceDownCardIndices ?? Array.Empty<int>();
    }

    /// <summary>
    /// Gets whether the variant allows drawing cards.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>True if draw is allowed.</returns>
    public bool AllowsDraw(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.AllowsDraw ?? false;
    }

    /// <summary>
    /// Gets the maximum number of cards that can be drawn.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The maximum draw count.</returns>
    public int GetMaxDrawCount(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.MaxDrawCount ?? 0;
    }

    /// <summary>
    /// Gets whether the variant is a hi/lo split game.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>True if it's a hi/lo game.</returns>
    public bool IsHiLoGame(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.IsHiLoGame ?? false;
    }

    /// <summary>
    /// Gets the recommended card size for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The recommended card size ("small", "medium", or "large").</returns>
    public string GetRecommendedCardSize(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.RecommendedCardSize ?? "medium";
    }

    /// <summary>
    /// Gets the recommended opponent card layout for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The recommended layout ("normal", "stacked", "overlapped", or "stud").</returns>
    public string GetRecommendedOpponentLayout(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.RecommendedOpponentLayout ?? "normal";
    }

    /// <summary>
    /// Gets the display type for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The variant display type.</returns>
    public VariantDisplayType GetDisplayType(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.DisplayType ?? VariantDisplayType.CommunityCards;
    }

    /// <summary>
    /// Gets the betting round names for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The names of the betting rounds.</returns>
    public IReadOnlyList<string> GetBettingRoundNames(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.BettingRoundNames ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the limit type abbreviation for a variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The limit type abbreviation (e.g., "NL", "PL", "FL").</returns>
    public string GetLimitTypeAbbreviation(PokerVariant variant)
    {
        return GetDisplayConfig(variant)?.LimitTypeAbbreviation ?? "NL";
    }

    /// <summary>
    /// Gets a formatted display name including limit type.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>A formatted display name like "NL Hold'em" or "PL Omaha".</returns>
    public string GetFormattedDisplayName(PokerVariant variant)
    {
        var config = GetDisplayConfig(variant);
        if (config == null)
        {
            return variant.ToString();
        }

        if (string.IsNullOrEmpty(config.LimitTypeAbbreviation))
        {
            return config.Name;
        }

        return $"{config.LimitTypeAbbreviation} {config.Name}";
    }

    /// <summary>
    /// Gets the number of face-down cards to show for opponents.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The number of face-down cards for opponents.</returns>
    public int GetOpponentFaceDownCount(PokerVariant variant)
    {
        var config = GetDisplayConfig(variant);
        if (config == null)
        {
            return 2; // Default for Hold'em style
        }

        if (config.IsStudGame)
        {
            return config.OpponentFaceDownCardCount;
        }

        // For community card games, all hole cards are face-down for opponents
        return config.HoleCardCount;
    }

    /// <summary>
    /// Gets the number of face-up cards to show for opponents.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>The number of face-up cards for opponents.</returns>
    public int GetOpponentFaceUpCount(PokerVariant variant)
    {
        var config = GetDisplayConfig(variant);
        if (config == null)
        {
            return 0; // Default for Hold'em style
        }

        if (config.IsStudGame)
        {
            return config.OpponentFaceUpCardCount;
        }

        // For community card games, no face-up cards for opponents until showdown
        return 0;
    }
}
