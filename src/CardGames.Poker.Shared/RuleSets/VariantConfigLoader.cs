using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.RuleSets;

/// <summary>
/// Provides variant-specific configuration loaded from RuleSetDto.
/// This service bridges the gap between the ruleset definitions and the game/UI logic.
/// </summary>
public class VariantConfigLoader
{
    private readonly RuleSetDto _ruleSet;

    public VariantConfigLoader(RuleSetDto ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        _ruleSet = ruleSet;
    }

    /// <summary>
    /// Creates a VariantConfigLoader for the specified poker variant.
    /// </summary>
    /// <param name="variant">The poker variant.</param>
    /// <returns>A configured VariantConfigLoader, or null if the variant is not found.</returns>
    public static VariantConfigLoader? ForVariant(PokerVariant variant)
    {
        var ruleSet = PredefinedRuleSets.GetByVariant(variant);
        return ruleSet != null ? new VariantConfigLoader(ruleSet) : null;
    }

    /// <summary>
    /// Creates a VariantConfigLoader from a RuleSetDto.
    /// </summary>
    /// <param name="ruleSet">The ruleset to load configuration from.</param>
    /// <returns>A configured VariantConfigLoader.</returns>
    public static VariantConfigLoader FromRuleSet(RuleSetDto ruleSet)
    {
        return new VariantConfigLoader(ruleSet);
    }

    /// <summary>
    /// Gets the underlying ruleset.
    /// </summary>
    public RuleSetDto RuleSet => _ruleSet;

    /// <summary>
    /// Gets the poker variant.
    /// </summary>
    public PokerVariant Variant => _ruleSet.Variant;

    /// <summary>
    /// Gets the display name of the variant.
    /// </summary>
    public string VariantName => _ruleSet.Name;

    /// <summary>
    /// Gets the description of the variant.
    /// </summary>
    public string? Description => _ruleSet.Description;

    /// <summary>
    /// Gets the betting limit type (NoLimit, PotLimit, FixedLimit).
    /// </summary>
    public LimitType LimitType => _ruleSet.LimitType;

    #region Card Configuration

    /// <summary>
    /// Gets the total number of hole cards dealt to each player.
    /// </summary>
    public int HoleCardCount => _ruleSet.HoleCardRules.Count;

    /// <summary>
    /// Gets the minimum number of hole cards that must be used in the final hand.
    /// </summary>
    public int MinHoleCardsUsed => _ruleSet.HoleCardRules.MinUsedInHand;

    /// <summary>
    /// Gets the maximum number of hole cards that can be used in the final hand.
    /// </summary>
    public int MaxHoleCardsUsed => _ruleSet.HoleCardRules.MaxUsedInHand;

    /// <summary>
    /// Gets whether the variant has community cards.
    /// </summary>
    public bool HasCommunityCards => _ruleSet.CommunityCardRules != null;

    /// <summary>
    /// Gets the total number of community cards (0 if no community cards).
    /// </summary>
    public int CommunityCardCount => _ruleSet.CommunityCardRules?.TotalCount ?? 0;

    /// <summary>
    /// Gets the minimum number of community cards that must be used in the final hand.
    /// </summary>
    public int MinCommunityCardsUsed => _ruleSet.CommunityCardRules?.MinUsedInHand ?? 0;

    /// <summary>
    /// Gets the maximum number of community cards that can be used in the final hand.
    /// </summary>
    public int MaxCommunityCardsUsed => _ruleSet.CommunityCardRules?.MaxUsedInHand ?? 0;

    #endregion

    #region Stud Game Configuration

    /// <summary>
    /// Gets whether this is a stud-style game with face-up and face-down cards.
    /// </summary>
    public bool IsStudGame => _ruleSet.CardVisibility.FaceUpIndices?.Count > 0 ||
                               _ruleSet.CardVisibility.FaceDownIndices?.Count > 0;

    /// <summary>
    /// Gets the indices of cards that are dealt face up (visible to all players).
    /// For stud games, these are typically cards 2-5 (0-indexed).
    /// </summary>
    public IReadOnlyList<int> FaceUpCardIndices =>
        _ruleSet.CardVisibility.FaceUpIndices ?? Array.Empty<int>();

    /// <summary>
    /// Gets the indices of cards that are dealt face down (private to the player).
    /// For stud games, these are typically cards 0, 1, and 6 (0-indexed).
    /// </summary>
    public IReadOnlyList<int> FaceDownCardIndices =>
        _ruleSet.CardVisibility.FaceDownIndices ?? Array.Empty<int>();

    /// <summary>
    /// Determines if a card at the given index should be shown face up.
    /// </summary>
    /// <param name="cardIndex">The 0-based index of the card.</param>
    /// <returns>True if the card should be face up, false otherwise.</returns>
    public bool IsCardFaceUp(int cardIndex)
    {
        return FaceUpCardIndices.Contains(cardIndex);
    }

    /// <summary>
    /// Gets the number of face-up cards for opponents (for display purposes).
    /// </summary>
    public int OpponentFaceUpCardCount => FaceUpCardIndices.Count;

    /// <summary>
    /// Gets the number of face-down cards for opponents (for display purposes).
    /// </summary>
    public int OpponentFaceDownCardCount => FaceDownCardIndices.Count;

    #endregion

    #region Draw Game Configuration

    /// <summary>
    /// Gets whether this variant allows drawing/exchanging cards.
    /// </summary>
    public bool AllowsDraw => _ruleSet.HoleCardRules.AllowDraw;

    /// <summary>
    /// Gets the maximum number of cards that can be drawn in a single draw phase.
    /// </summary>
    public int MaxDrawCount => _ruleSet.HoleCardRules.MaxDrawCount;

    #endregion

    #region Betting Configuration

    /// <summary>
    /// Gets whether the variant uses antes.
    /// </summary>
    public bool HasAnte => _ruleSet.AnteBlindRules?.HasAnte ?? false;

    /// <summary>
    /// Gets the ante percentage relative to the big blind.
    /// </summary>
    public decimal AntePercentage => _ruleSet.AnteBlindRules?.AntePercentage ?? 0;

    /// <summary>
    /// Gets whether the variant uses a small blind.
    /// </summary>
    public bool HasSmallBlind => _ruleSet.AnteBlindRules?.HasSmallBlind ?? true;

    /// <summary>
    /// Gets whether the variant uses a big blind.
    /// </summary>
    public bool HasBigBlind => _ruleSet.AnteBlindRules?.HasBigBlind ?? true;

    /// <summary>
    /// Gets whether straddles are allowed.
    /// </summary>
    public bool AllowStraddle => _ruleSet.AnteBlindRules?.AllowStraddle ?? false;

    /// <summary>
    /// Gets whether button ante is used.
    /// </summary>
    public bool HasButtonAnte => _ruleSet.AnteBlindRules?.ButtonAnte ?? false;

    /// <summary>
    /// Gets the betting rounds configuration.
    /// </summary>
    public IReadOnlyList<BettingRoundDto> BettingRounds => _ruleSet.BettingRounds;

    /// <summary>
    /// Gets the number of betting rounds.
    /// </summary>
    public int NumberOfBettingRounds => _ruleSet.BettingRounds.Count;

    /// <summary>
    /// Gets the name of a betting round by its order.
    /// </summary>
    /// <param name="roundOrder">The 0-based order of the betting round.</param>
    /// <returns>The name of the betting round, or null if not found.</returns>
    public string? GetBettingRoundName(int roundOrder)
    {
        return _ruleSet.BettingRounds.FirstOrDefault(r => r.Order == roundOrder)?.Name;
    }

    /// <summary>
    /// Gets the min bet multiplier for a betting round.
    /// </summary>
    /// <param name="roundOrder">The 0-based order of the betting round.</param>
    /// <returns>The min bet multiplier (typically 0.5 for early streets in fixed limit, 1.0 otherwise).</returns>
    public decimal GetMinBetMultiplier(int roundOrder)
    {
        return _ruleSet.BettingRounds.FirstOrDefault(r => r.Order == roundOrder)?.MinBetMultiplier ?? 1.0m;
    }

    /// <summary>
    /// Determines if this round uses the big bet (for fixed limit games).
    /// Returns true for later streets (typically turn/river in hold'em style games).
    /// </summary>
    /// <param name="roundOrder">The 0-based order of the betting round.</param>
    /// <returns>True if the big bet should be used, false for small bet.</returns>
    public bool UsesBigBet(int roundOrder)
    {
        if (LimitType != LimitType.FixedLimit)
        {
            return false;
        }

        var round = _ruleSet.BettingRounds.FirstOrDefault(r => r.Order == roundOrder);
        return round?.MinBetMultiplier >= 1.0m;
    }

    #endregion

    #region Hi/Lo Split Configuration

    /// <summary>
    /// Gets whether this is a hi/lo split pot game.
    /// </summary>
    public bool IsHiLoGame => _ruleSet.HiLoRules?.Enabled ?? false;

    /// <summary>
    /// Gets the qualifier for the low hand (e.g., 8 for eight-or-better).
    /// </summary>
    public int LowQualifier => _ruleSet.HiLoRules?.LowQualifier ?? 8;

    /// <summary>
    /// Gets whether an ace can be used as a low card.
    /// </summary>
    public bool AcePlaysLow => _ruleSet.HiLoRules?.AcePlaysLow ?? true;

    /// <summary>
    /// Gets whether straights and flushes count against the low hand.
    /// </summary>
    public bool StraightsFlushesCountAgainstLow =>
        _ruleSet.HiLoRules?.StraightsFlushesCountAgainstLow ?? false;

    #endregion

    #region Wildcard Configuration

    /// <summary>
    /// Gets whether wildcards are used in this variant.
    /// </summary>
    public bool HasWildcards => _ruleSet.WildcardRules?.Enabled ?? false;

    /// <summary>
    /// Gets the list of wildcard cards (e.g., ["Kh", "Kd", "Kc", "Ks"] for kings wild).
    /// </summary>
    public IReadOnlyList<string> WildcardCards =>
        _ruleSet.WildcardRules?.WildcardCards ?? Array.Empty<string>();

    /// <summary>
    /// Gets whether the wildcard designation can change during play.
    /// </summary>
    public bool HasDynamicWildcards => _ruleSet.WildcardRules?.Dynamic ?? false;

    /// <summary>
    /// Gets the rule description for dynamic wildcards.
    /// </summary>
    public string? DynamicWildcardRule => _ruleSet.WildcardRules?.DynamicRule;

    #endregion

    #region Showdown Configuration

    /// <summary>
    /// Gets the order in which hands are shown at showdown.
    /// </summary>
    public ShowdownOrder ShowdownOrder =>
        _ruleSet.ShowdownRules?.ShowOrder ?? ShowdownOrder.LastAggressor;

    /// <summary>
    /// Gets whether players can muck their cards without showing.
    /// </summary>
    public bool AllowMuck => _ruleSet.ShowdownRules?.AllowMuck ?? true;

    /// <summary>
    /// Gets whether all hands must be shown when there's been all-in action.
    /// </summary>
    public bool ShowAllOnAllIn => _ruleSet.ShowdownRules?.ShowAllOnAllIn ?? true;

    #endregion

    #region Special Rules

    /// <summary>
    /// Gets the list of special rules for this variant.
    /// </summary>
    public IReadOnlyList<SpecialRuleDto> SpecialRules =>
        _ruleSet.SpecialRules ?? Array.Empty<SpecialRuleDto>();

    /// <summary>
    /// Gets whether a specific special rule is enabled.
    /// </summary>
    /// <param name="ruleId">The ID of the special rule to check.</param>
    /// <returns>True if the rule is enabled, false otherwise.</returns>
    public bool IsSpecialRuleEnabled(string ruleId)
    {
        return _ruleSet.SpecialRules?.Any(r => r.Id == ruleId && r.Enabled) ?? false;
    }

    /// <summary>
    /// Gets whether the bring-in rule is enabled (for stud games).
    /// </summary>
    public bool HasBringIn => IsSpecialRuleEnabled("bring-in");

    /// <summary>
    /// Gets whether the draw phase rule is enabled (for draw games).
    /// </summary>
    public bool HasDrawPhase => IsSpecialRuleEnabled("draw-phase") || AllowsDraw;

    /// <summary>
    /// Gets whether this variant has the "losers match pot" rule.
    /// </summary>
    public bool HasLosersMatchPot => IsSpecialRuleEnabled("losers-match-pot");

    /// <summary>
    /// Gets whether this variant has the "drop or stay" rule.
    /// </summary>
    public bool HasDropOrStay => IsSpecialRuleEnabled("drop-or-stay");

    #endregion

    #region UI Configuration Helpers

    /// <summary>
    /// Gets the variant type for display purposes.
    /// </summary>
    public VariantDisplayType DisplayType
    {
        get
        {
            if (HasCommunityCards)
            {
                return VariantDisplayType.CommunityCards;
            }

            if (IsStudGame)
            {
                return VariantDisplayType.Stud;
            }

            if (AllowsDraw)
            {
                return VariantDisplayType.Draw;
            }

            return VariantDisplayType.Other;
        }
    }

    /// <summary>
    /// Gets the appropriate card size based on the number of hole cards.
    /// </summary>
    public string RecommendedCardSize
    {
        get
        {
            return HoleCardCount switch
            {
                <= 2 => "large",
                <= 4 => "medium",
                _ => "small"
            };
        }
    }

    /// <summary>
    /// Gets the appropriate layout for opponent cards.
    /// </summary>
    public string RecommendedOpponentLayout
    {
        get
        {
            if (IsStudGame)
            {
                return "stud"; // Shows face-up/face-down cards
            }

            return HoleCardCount switch
            {
                <= 2 => "normal",
                <= 4 => "stacked",
                _ => "overlapped"
            };
        }
    }

    /// <summary>
    /// Converts the configuration to a VariantDisplayConfigDto for UI consumption.
    /// </summary>
    /// <returns>A DTO containing the display configuration.</returns>
    public VariantDisplayConfigDto ToDisplayConfig()
    {
        return new VariantDisplayConfigDto
        {
            Variant = Variant,
            Name = VariantName,
            Description = Description,
            LimitType = LimitType,
            LimitTypeName = GetLimitTypeDisplayName(),
            LimitTypeAbbreviation = GetLimitTypeAbbreviation(),
            HoleCardCount = HoleCardCount,
            HasCommunityCards = HasCommunityCards,
            CommunityCardCount = CommunityCardCount,
            IsStudGame = IsStudGame,
            FaceUpCardIndices = FaceUpCardIndices.ToList(),
            FaceDownCardIndices = FaceDownCardIndices.ToList(),
            OpponentFaceUpCardCount = OpponentFaceUpCardCount,
            OpponentFaceDownCardCount = OpponentFaceDownCardCount,
            AllowsDraw = AllowsDraw,
            MaxDrawCount = MaxDrawCount,
            IsHiLoGame = IsHiLoGame,
            LowQualifier = LowQualifier,
            HasWildcards = HasWildcards,
            WildcardDescription = HasDynamicWildcards ? DynamicWildcardRule : 
                (WildcardCards.Count > 0 ? $"Wild cards: {string.Join(", ", WildcardCards)}" : null),
            HasAnte = HasAnte,
            HasSmallBlind = HasSmallBlind,
            HasBigBlind = HasBigBlind,
            HasBringIn = HasBringIn,
            BettingRoundNames = BettingRounds.Select(r => r.Name).ToList(),
            RecommendedCardSize = RecommendedCardSize,
            RecommendedOpponentLayout = RecommendedOpponentLayout,
            DisplayType = DisplayType
        };
    }

    /// <summary>
    /// Gets a display name for the limit type.
    /// </summary>
    private string GetLimitTypeDisplayName()
    {
        return LimitType switch
        {
            LimitType.NoLimit => "No Limit",
            LimitType.PotLimit => "Pot Limit",
            LimitType.FixedLimit => "Fixed Limit",
            LimitType.SpreadLimit => "Spread Limit",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets an abbreviation for the limit type.
    /// </summary>
    private string GetLimitTypeAbbreviation()
    {
        return LimitType switch
        {
            LimitType.NoLimit => "NL",
            LimitType.PotLimit => "PL",
            LimitType.FixedLimit => "FL",
            LimitType.SpreadLimit => "SL",
            _ => ""
        };
    }

    #endregion
}
