namespace CardGames.Poker.Games;

/// <summary>
/// Represents the current phase of a Seven Card Stud hand.
/// </summary>
public enum SevenCardStudPhase
{
    /// <summary>Waiting for hand to start.</summary>
    WaitingToStart,

    /// <summary>Collecting antes from all players.</summary>
    CollectingAntes,

    /// <summary>Third street: 2 down cards and 1 up card dealt, bring-in betting round.</summary>
    ThirdStreet,

    /// <summary>Fourth street: 1 up card dealt, betting round (small bet).</summary>
    FourthStreet,

    /// <summary>Fifth street: 1 up card dealt, betting round (big bet).</summary>
    FifthStreet,

    /// <summary>Sixth street: 1 up card dealt, betting round (big bet).</summary>
    SixthStreet,

    /// <summary>Seventh street (river): 1 down card dealt, final betting round (big bet).</summary>
    SeventhStreet,

    /// <summary>Showdown - comparing hands to determine winner.</summary>
    Showdown,

    /// <summary>Hand is complete.</summary>
    Complete
}
