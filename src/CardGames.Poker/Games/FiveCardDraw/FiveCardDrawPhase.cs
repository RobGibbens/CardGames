namespace CardGames.Poker.Games.FiveCardDraw;

/// <summary>
/// Represents the current phase of a Five Card Draw hand.
/// </summary>
public enum FiveCardDrawPhase
{
    /// <summary>Waiting for hand to start.</summary>
    WaitingToStart,

    /// <summary>Collecting antes from all players.</summary>
    CollectingAntes,

    /// <summary>Dealing initial cards to players.</summary>
    Dealing,

    /// <summary>First betting round (pre-draw).</summary>
    FirstBettingRound,

    /// <summary>Draw phase where players can discard and draw cards.</summary>
    DrawPhase,

    /// <summary>Second betting round (post-draw).</summary>
    SecondBettingRound,

    /// <summary>Showdown - comparing hands to determine winner.</summary>
    Showdown,

    /// <summary>Hand is complete.</summary>
    Complete
}
