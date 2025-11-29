using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs.RuleSets;

/// <summary>
/// Represents a canonical, versioned ruleset schema for poker variant configuration.
/// </summary>
public record RuleSetDto(
    /// <summary>
    /// Schema version for backward compatibility.
    /// </summary>
    string SchemaVersion,

    /// <summary>
    /// Unique identifier for the ruleset.
    /// </summary>
    string Id,

    /// <summary>
    /// Display name of the poker variant.
    /// </summary>
    string Name,

    /// <summary>
    /// The poker variant type.
    /// </summary>
    PokerVariant Variant,

    /// <summary>
    /// Deck composition configuration.
    /// </summary>
    DeckCompositionDto DeckComposition,

    /// <summary>
    /// Card visibility rules.
    /// </summary>
    CardVisibilityDto CardVisibility,

    /// <summary>
    /// Betting rounds configuration.
    /// </summary>
    IReadOnlyList<BettingRoundDto> BettingRounds,

    /// <summary>
    /// Hole card configuration.
    /// </summary>
    HoleCardRulesDto HoleCardRules,

    /// <summary>
    /// Community card configuration (null for non-community card games).
    /// </summary>
    CommunityCardRulesDto? CommunityCardRules = null,

    /// <summary>
    /// Ante and blind rules.
    /// </summary>
    AnteBlindRulesDto? AnteBlindRules = null,

    /// <summary>
    /// Betting limit type.
    /// </summary>
    LimitType LimitType = LimitType.NoLimit,

    /// <summary>
    /// Wildcard configuration (null if no wildcards).
    /// </summary>
    WildcardRulesDto? WildcardRules = null,

    /// <summary>
    /// Showdown rules.
    /// </summary>
    ShowdownRulesDto? ShowdownRules = null,

    /// <summary>
    /// Hi/Lo split pot rules (null if not a hi/lo game).
    /// </summary>
    HiLoRulesDto? HiLoRules = null,

    /// <summary>
    /// Special variant-specific rules.
    /// </summary>
    IReadOnlyList<SpecialRuleDto>? SpecialRules = null,

    /// <summary>
    /// Optional description of the variant.
    /// </summary>
    string? Description = null);

/// <summary>
/// Represents deck composition configuration.
/// </summary>
public record DeckCompositionDto(
    /// <summary>
    /// Type of deck (Full52, Short36, Custom).
    /// </summary>
    DeckType DeckType,

    /// <summary>
    /// Number of decks to use.
    /// </summary>
    int NumberOfDecks = 1,

    /// <summary>
    /// Cards to exclude from the deck (e.g., ["2h", "2d", "2c", "2s"] for short deck).
    /// </summary>
    IReadOnlyList<string>? ExcludedCards = null,

    /// <summary>
    /// Cards to include in the deck for custom configurations.
    /// </summary>
    IReadOnlyList<string>? IncludedCards = null);

/// <summary>
/// Represents card visibility rules.
/// </summary>
public record CardVisibilityDto(
    /// <summary>
    /// Whether hole cards are visible only to their owner.
    /// </summary>
    bool HoleCardsPrivate = true,

    /// <summary>
    /// Whether community cards are visible to all players.
    /// </summary>
    bool CommunityCardsPublic = true,

    /// <summary>
    /// For stud games: indices of cards dealt face up.
    /// </summary>
    IReadOnlyList<int>? FaceUpIndices = null,

    /// <summary>
    /// For stud games: indices of cards dealt face down.
    /// </summary>
    IReadOnlyList<int>? FaceDownIndices = null);

/// <summary>
/// Represents a betting round configuration.
/// </summary>
public record BettingRoundDto(
    /// <summary>
    /// Name of the betting round (e.g., "Preflop", "Flop", "Turn", "River").
    /// </summary>
    string Name,

    /// <summary>
    /// Order of the betting round (0-based).
    /// </summary>
    int Order,

    /// <summary>
    /// Number of community cards dealt at the start of this round.
    /// </summary>
    int CommunityCardsDealt = 0,

    /// <summary>
    /// Number of hole cards dealt at the start of this round.
    /// </summary>
    int HoleCardsDealt = 0,

    /// <summary>
    /// Whether cards are dealt face up (for stud games).
    /// </summary>
    bool DealtFaceUp = false,

    /// <summary>
    /// Minimum bet size multiplier relative to big blind.
    /// </summary>
    decimal MinBetMultiplier = 1.0m,

    /// <summary>
    /// Maximum number of raises allowed in this round (null for unlimited).
    /// </summary>
    int? MaxRaises = null);

/// <summary>
/// Represents hole card rules.
/// </summary>
public record HoleCardRulesDto(
    /// <summary>
    /// Total number of hole cards dealt to each player.
    /// </summary>
    int Count,

    /// <summary>
    /// Minimum number of hole cards that must be used in the final hand.
    /// </summary>
    int MinUsedInHand = 0,

    /// <summary>
    /// Maximum number of hole cards that can be used in the final hand.
    /// </summary>
    int MaxUsedInHand = 5,

    /// <summary>
    /// Whether players can draw/exchange cards.
    /// </summary>
    bool AllowDraw = false,

    /// <summary>
    /// Maximum number of cards that can be drawn (for draw games).
    /// </summary>
    int MaxDrawCount = 0);

/// <summary>
/// Represents community card rules.
/// </summary>
public record CommunityCardRulesDto(
    /// <summary>
    /// Total number of community cards.
    /// </summary>
    int TotalCount,

    /// <summary>
    /// Minimum number of community cards that must be used in the final hand.
    /// </summary>
    int MinUsedInHand = 0,

    /// <summary>
    /// Maximum number of community cards that can be used in the final hand.
    /// </summary>
    int MaxUsedInHand = 5);

/// <summary>
/// Represents ante and blind rules.
/// </summary>
public record AnteBlindRulesDto(
    /// <summary>
    /// Whether antes are required.
    /// </summary>
    bool HasAnte = false,

    /// <summary>
    /// Ante amount as a percentage of big blind.
    /// </summary>
    decimal AntePercentage = 0,

    /// <summary>
    /// Whether small blind is required.
    /// </summary>
    bool HasSmallBlind = true,

    /// <summary>
    /// Whether big blind is required.
    /// </summary>
    bool HasBigBlind = true,

    /// <summary>
    /// Whether straddles are allowed.
    /// </summary>
    bool AllowStraddle = false,

    /// <summary>
    /// Whether button ante is used instead of rotating antes.
    /// </summary>
    bool ButtonAnte = false);

/// <summary>
/// Represents wildcard rules.
/// </summary>
public record WildcardRulesDto(
    /// <summary>
    /// Whether wildcards are used in this variant.
    /// </summary>
    bool Enabled = false,

    /// <summary>
    /// Cards designated as wildcards (e.g., ["2h", "2d", "2c", "2s"] for deuces wild).
    /// </summary>
    IReadOnlyList<string>? WildcardCards = null,

    /// <summary>
    /// Whether the wildcard designation can change during play.
    /// </summary>
    bool Dynamic = false,

    /// <summary>
    /// Description of how wildcards are determined (for dynamic wildcards).
    /// </summary>
    string? DynamicRule = null);

/// <summary>
/// Represents showdown rules.
/// </summary>
public record ShowdownRulesDto(
    /// <summary>
    /// Order in which hands are shown (LastAggressor, ClockwiseFromButton, etc.).
    /// </summary>
    ShowdownOrder ShowOrder = ShowdownOrder.LastAggressor,

    /// <summary>
    /// Whether players can muck losing hands without showing.
    /// </summary>
    bool AllowMuck = true,

    /// <summary>
    /// Whether all hands must be shown if there's been all-in action.
    /// </summary>
    bool ShowAllOnAllIn = true);

/// <summary>
/// Represents Hi/Lo split pot rules.
/// </summary>
public record HiLoRulesDto(
    /// <summary>
    /// Whether this is a hi/lo split game.
    /// </summary>
    bool Enabled = false,

    /// <summary>
    /// Qualifier for low hand (8 = eight-or-better, 0 = no qualifier).
    /// </summary>
    int LowQualifier = 8,

    /// <summary>
    /// Whether ace can be used as low.
    /// </summary>
    bool AcePlaysLow = true,

    /// <summary>
    /// Whether straights and flushes count against low.
    /// </summary>
    bool StraightsFlushesCountAgainstLow = false);

/// <summary>
/// Represents a special variant-specific rule.
/// </summary>
public record SpecialRuleDto(
    /// <summary>
    /// Unique identifier for the rule.
    /// </summary>
    string Id,

    /// <summary>
    /// Display name of the rule.
    /// </summary>
    string Name,

    /// <summary>
    /// Description of what the rule does.
    /// </summary>
    string Description,

    /// <summary>
    /// Whether this rule is active.
    /// </summary>
    bool Enabled = true);
