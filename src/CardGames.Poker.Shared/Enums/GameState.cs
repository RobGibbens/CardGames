namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the current state of a poker game/table.
/// </summary>
public enum GameState
{
    /// <summary>Game is waiting for players to join.</summary>
    WaitingForPlayers,

    /// <summary>Game is ready to start with enough players.</summary>
    ReadyToStart,

    /// <summary>Game is collecting antes or blinds.</summary>
    CollectingBlinds,

    /// <summary>Cards are being dealt to players.</summary>
    Dealing,

    /// <summary>Betting round is in progress.</summary>
    BettingRound,

    /// <summary>Community cards are being dealt (Hold'em/Omaha).</summary>
    DealingCommunityCards,

    /// <summary>Showdown phase - revealing cards to determine winner.</summary>
    Showdown,

    /// <summary>Hand is complete, pot being awarded.</summary>
    HandComplete,

    /// <summary>Game is paused.</summary>
    Paused,

    /// <summary>Game has ended.</summary>
    Ended
}
