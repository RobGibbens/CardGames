namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// The type of blind being posted.
/// </summary>
public enum BlindTypeDto
{
    /// <summary>No blind.</summary>
    None,

    /// <summary>Small blind.</summary>
    SmallBlind,

    /// <summary>Big blind.</summary>
    BigBlind,

    /// <summary>Ante.</summary>
    Ante,

    /// <summary>Bring-in (for stud games).</summary>
    BringIn,

    /// <summary>Missed small blind that must be posted.</summary>
    MissedSmallBlind,

    /// <summary>Missed big blind that must be posted.</summary>
    MissedBigBlind,

    /// <summary>Button straddle.</summary>
    Straddle
}
