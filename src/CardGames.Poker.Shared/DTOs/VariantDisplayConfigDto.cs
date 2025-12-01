using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Provides display configuration for a poker variant.
/// Used by UI components to adapt to different game types.
/// </summary>
public record VariantDisplayConfigDto
{
    /// <summary>
    /// The poker variant.
    /// </summary>
    public PokerVariant Variant { get; init; }

    /// <summary>
    /// Display name of the variant.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Description of the variant.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The betting limit type.
    /// </summary>
    public LimitType LimitType { get; init; }

    /// <summary>
    /// Display name for the limit type (e.g., "No Limit", "Pot Limit").
    /// </summary>
    public string LimitTypeName { get; init; } = string.Empty;

    /// <summary>
    /// Abbreviated limit type (e.g., "NL", "PL", "FL").
    /// </summary>
    public string LimitTypeAbbreviation { get; init; } = string.Empty;

    #region Card Display Configuration

    /// <summary>
    /// Number of hole cards per player.
    /// </summary>
    public int HoleCardCount { get; init; }

    /// <summary>
    /// Whether the variant has community cards.
    /// </summary>
    public bool HasCommunityCards { get; init; }

    /// <summary>
    /// Total number of community cards.
    /// </summary>
    public int CommunityCardCount { get; init; }

    /// <summary>
    /// Whether this is a stud-style game.
    /// </summary>
    public bool IsStudGame { get; init; }

    /// <summary>
    /// Indices of face-up cards for stud games.
    /// </summary>
    public IReadOnlyList<int> FaceUpCardIndices { get; init; } = [];

    /// <summary>
    /// Indices of face-down cards for stud games.
    /// </summary>
    public IReadOnlyList<int> FaceDownCardIndices { get; init; } = [];

    /// <summary>
    /// Number of face-up cards to show for opponents.
    /// </summary>
    public int OpponentFaceUpCardCount { get; init; }

    /// <summary>
    /// Number of face-down cards to show for opponents.
    /// </summary>
    public int OpponentFaceDownCardCount { get; init; }

    #endregion

    #region Draw Game Configuration

    /// <summary>
    /// Whether the variant allows drawing cards.
    /// </summary>
    public bool AllowsDraw { get; init; }

    /// <summary>
    /// Maximum number of cards that can be discarded/drawn.
    /// </summary>
    public int MaxDrawCount { get; init; }

    #endregion

    #region Hi/Lo Split Configuration

    /// <summary>
    /// Whether this is a hi/lo split pot game.
    /// </summary>
    public bool IsHiLoGame { get; init; }

    /// <summary>
    /// The qualifier for the low hand (e.g., 8 for eight-or-better).
    /// </summary>
    public int LowQualifier { get; init; }

    #endregion

    #region Wildcard Configuration

    /// <summary>
    /// Whether wildcards are used.
    /// </summary>
    public bool HasWildcards { get; init; }

    /// <summary>
    /// Description of wildcard rules.
    /// </summary>
    public string? WildcardDescription { get; init; }

    #endregion

    #region Betting Configuration

    /// <summary>
    /// Whether the variant uses antes.
    /// </summary>
    public bool HasAnte { get; init; }

    /// <summary>
    /// Whether the variant uses a small blind.
    /// </summary>
    public bool HasSmallBlind { get; init; }

    /// <summary>
    /// Whether the variant uses a big blind.
    /// </summary>
    public bool HasBigBlind { get; init; }

    /// <summary>
    /// Whether the variant uses a bring-in bet.
    /// </summary>
    public bool HasBringIn { get; init; }

    /// <summary>
    /// Names of the betting rounds.
    /// </summary>
    public IReadOnlyList<string> BettingRoundNames { get; init; } = [];

    #endregion

    #region UI Layout Configuration

    /// <summary>
    /// Recommended card size for the variant.
    /// </summary>
    public string RecommendedCardSize { get; init; } = "medium";

    /// <summary>
    /// Recommended layout for opponent cards.
    /// </summary>
    public string RecommendedOpponentLayout { get; init; } = "normal";

    /// <summary>
    /// Display type of the variant for UI decisions.
    /// </summary>
    public VariantDisplayType DisplayType { get; init; }

    #endregion
}

/// <summary>
/// Represents the display type of a poker variant for UI purposes.
/// </summary>
public enum VariantDisplayType
{
    /// <summary>
    /// Community card games like Hold'em and Omaha.
    /// </summary>
    CommunityCards,

    /// <summary>
    /// Stud games with face-up and face-down cards.
    /// </summary>
    Stud,

    /// <summary>
    /// Draw games where players can exchange cards.
    /// </summary>
    Draw,

    /// <summary>
    /// Other variant types.
    /// </summary>
    Other
}
