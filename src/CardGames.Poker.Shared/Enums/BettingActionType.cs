namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the possible betting actions a player can take during a betting round.
/// </summary>
public enum BettingActionType
{
    /// <summary>Pass the action without betting when no bet has been made.</summary>
    Check,

    /// <summary>Make an initial bet in the current betting round.</summary>
    Bet,

    /// <summary>Match the current highest bet.</summary>
    Call,

    /// <summary>Increase the current bet amount.</summary>
    Raise,

    /// <summary>Give up the hand and forfeit any chips already in the pot.</summary>
    Fold,

    /// <summary>Bet all remaining chips.</summary>
    AllIn,

    /// <summary>Post ante or blind (forced bet).</summary>
    Post
}
