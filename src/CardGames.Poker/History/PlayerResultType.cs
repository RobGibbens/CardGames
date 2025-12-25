namespace CardGames.Poker.History;

/// <summary>
/// Indicates the final outcome for a player in a completed hand.
/// </summary>
public enum PlayerResultType
{
    /// <summary>
    /// Player folded before showdown.
    /// </summary>
    Folded = 0,

    /// <summary>
    /// Player won the hand (including via fold or showdown).
    /// </summary>
    Won = 1,

    /// <summary>
    /// Player lost at showdown.
    /// </summary>
    Lost = 2,

    /// <summary>
    /// Player won a split pot (tied with one or more other players).
    /// </summary>
    SplitPotWon = 3
}
