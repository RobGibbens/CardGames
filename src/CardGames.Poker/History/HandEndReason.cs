namespace CardGames.Poker.History;

/// <summary>
/// Indicates how a poker hand terminated.
/// </summary>
public enum HandEndReason
{
    /// <summary>
    /// All other players folded, leaving one winner.
    /// </summary>
    FoldedToWinner = 0,

    /// <summary>
    /// Multiple players reached the showdown phase.
    /// </summary>
    Showdown = 1
}
