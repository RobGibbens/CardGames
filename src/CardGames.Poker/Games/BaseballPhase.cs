namespace CardGames.Poker.Games;

/// <summary>
/// Represents the current phase of a Baseball hand.
/// Baseball is a seven-card stud variant where 3s and 9s are wild,
/// and 4s dealt face up grant extra face-down cards for a fixed buy-card price.
/// </summary>
public enum BaseballPhase
{
    /// <summary>Waiting for hand to start.</summary>
    WaitingToStart,

    /// <summary>Collecting antes from all players.</summary>
    CollectingAntes,

    /// <summary>Third street: 2 down cards and 1 up card dealt, bring-in betting round.</summary>
    ThirdStreet,

    /// <summary>Offering buy-card option to players who received a 4 face up.</summary>
    BuyCardOffer,

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
