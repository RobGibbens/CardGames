namespace CardGames.Poker.Games.GameFlow;

/// <summary>
/// Categories of game phases for routing and UI purposes.
/// Used to classify phases for programmatic handling and display.
/// </summary>
public enum PhaseCategory
{
    /// <summary>
    /// Setup phases (WaitingToStart, WaitingForPlayers).
    /// </summary>
    Setup,

    /// <summary>
    /// Ante/blind collection phases.
    /// </summary>
    Collection,

    /// <summary>
    /// Card dealing phases.
    /// </summary>
    Dealing,

    /// <summary>
    /// Betting phases (FirstBettingRound, ThirdStreet, etc.).
    /// </summary>
    Betting,

    /// <summary>
    /// Drawing/discard phases.
    /// </summary>
    Drawing,

    /// <summary>
    /// Decision phases (DropOrStay).
    /// </summary>
    Decision,

    /// <summary>
    /// Special game-specific phases (PotMatching, PlayerVsDeck, BuyCard).
    /// </summary>
    Special,

    /// <summary>
    /// Resolution phases (Showdown, Complete).
    /// </summary>
    Resolution
}
