using System.Collections.Generic;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Games;

/// <summary>
/// Configuration for wild cards in a Dealer's Choice game.
/// </summary>
public class WildCardConfiguration
{
    /// <summary>
    /// Whether wild cards are enabled for this hand.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The type of wild card rule to apply.
    /// </summary>
    public WildCardType Type { get; set; } = WildCardType.None;

    /// <summary>
    /// Custom wild card values (when Type is Custom).
    /// Values are card values (2-14, where 14 is Ace).
    /// </summary>
    public List<int> CustomWildValues { get; set; } = [];

    /// <summary>
    /// Whether the lowest card in each player's hand is wild.
    /// </summary>
    public bool LowCardsWild { get; set; }

    /// <summary>
    /// Creates a configuration with no wild cards.
    /// </summary>
    public static WildCardConfiguration None() => new() { Enabled = false, Type = WildCardType.None };

    /// <summary>
    /// Creates a configuration with deuces wild.
    /// </summary>
    public static WildCardConfiguration DeucesWild() => new() { Enabled = true, Type = WildCardType.DeucesWild };

    /// <summary>
    /// Creates a configuration with one-eyed jacks wild.
    /// </summary>
    public static WildCardConfiguration OneEyedJacksWild() => new() { Enabled = true, Type = WildCardType.OneEyedJacks };

    /// <summary>
    /// Creates a configuration with jokers wild (requires joker cards in deck).
    /// </summary>
    public static WildCardConfiguration JokersWild() => new() { Enabled = true, Type = WildCardType.JokersWild };
}

/// <summary>
/// Types of wild card rules that can be applied.
/// </summary>
public enum WildCardType
{
    /// <summary>No wild cards.</summary>
    None,

    /// <summary>All 2s are wild.</summary>
    DeucesWild,

    /// <summary>All 3s and 9s are wild (Baseball style).</summary>
    ThreesAndNines,

    /// <summary>All Kings are wild.</summary>
    KingsWild,

    /// <summary>All Queens are wild.</summary>
    QueensWild,

    /// <summary>All Jacks are wild. Note: Traditional one-eyed jacks (Jh, Js only) is not yet supported.</summary>
    OneEyedJacks,

    /// <summary>All Jacks are wild.</summary>
    JacksWild,

    /// <summary>Jokers are wild (requires joker cards in deck).</summary>
    JokersWild,

    /// <summary>Custom wild cards specified by value.</summary>
    Custom
}

/// <summary>
/// Configuration for a hand in Dealer's Choice.
/// </summary>
public class DealersChoiceHandConfig
{
    /// <summary>
    /// The variant selected by the dealer for this hand.
    /// </summary>
    public PokerVariant Variant { get; set; }

    /// <summary>
    /// The wild card configuration for this hand.
    /// </summary>
    public WildCardConfiguration WildCards { get; set; } = WildCardConfiguration.None();

    /// <summary>
    /// Additional variant-specific options.
    /// </summary>
    public Dictionary<string, object> VariantOptions { get; set; } = [];
}
