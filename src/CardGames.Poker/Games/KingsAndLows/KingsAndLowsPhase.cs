namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Represents the current phase of a Kings and Lows hand.
/// </summary>
public enum KingsAndLowsPhase
{
    /// <summary>Waiting for hand to start.</summary>
    WaitingToStart,

    /// <summary>Collecting antes from all players.</summary>
    CollectingAntes,

    /// <summary>Dealing initial 5 cards to all players.</summary>
    Dealing,

    /// <summary>Drop-or-stay decision phase where players simultaneously decide to drop or stay.</summary>
    DropOrStay,

    /// <summary>Draw phase where staying players discard and draw cards.</summary>
    DrawPhase,

    /// <summary>
    /// Draw complete - all players have drawn their cards.
    /// This is a brief display phase before showdown begins.
    /// </summary>
    DrawComplete,

    /// <summary>
    /// Special case: Only one player stayed - they play against the deck.
    /// Dealer deals a dummy hand from remaining cards.
    /// </summary>
    PlayerVsDeck,

    /// <summary>Showdown - comparing hands to determine winner.</summary>
    Showdown,

    /// <summary>
    /// Losers must match the pot. This phase handles pot matching.
    /// </summary>
    PotMatching,

    /// <summary>Hand is complete.</summary>
    Complete
}
