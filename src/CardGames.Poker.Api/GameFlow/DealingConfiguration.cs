namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Describes how cards are dealt for a specific poker variant.
/// Used by <see cref="IGameFlowHandler"/> to provide dealing behavior.
/// </summary>
public sealed class DealingConfiguration
{
    /// <summary>
    /// Gets or sets the dealing pattern type.
    /// </summary>
    public required DealingPatternType PatternType { get; init; }

    /// <summary>
    /// Gets or sets the initial cards dealt to each player (for draw games).
    /// </summary>
    public int InitialCardsPerPlayer { get; init; }

    /// <summary>
    /// Gets or sets the dealing rounds for stud games.
    /// </summary>
    public IReadOnlyList<DealingRoundConfig>? DealingRounds { get; init; }

    /// <summary>
    /// Gets or sets whether all cards are dealt face down initially.
    /// </summary>
    public bool AllFaceDown { get; init; } = true;
}

/// <summary>
/// Specifies the pattern type for dealing cards.
/// </summary>
public enum DealingPatternType
{
    /// <summary>
    /// All cards dealt at once (Five Card Draw, Kings and Lows).
    /// </summary>
    AllAtOnce,

    /// <summary>
    /// Cards dealt in rounds with betting between (Seven Card Stud).
    /// </summary>
    StreetBased,

    /// <summary>
    /// Community cards dealt in stages (Hold'em, Omaha).
    /// </summary>
    CommunityCard
}

/// <summary>
/// Describes a single round of dealing for street-based games.
/// </summary>
public sealed class DealingRoundConfig
{
    /// <summary>
    /// Gets or sets the phase name associated with this dealing round.
    /// </summary>
    public required string PhaseName { get; init; }

    /// <summary>
    /// Gets or sets the number of hole (face-down) cards dealt in this round.
    /// </summary>
    public required int HoleCards { get; init; }

    /// <summary>
    /// Gets or sets the number of board (face-up) cards dealt in this round.
    /// </summary>
    public required int BoardCards { get; init; }

    /// <summary>
    /// Gets or sets whether betting occurs after this dealing round.
    /// </summary>
    public required bool HasBettingAfter { get; init; }
}
