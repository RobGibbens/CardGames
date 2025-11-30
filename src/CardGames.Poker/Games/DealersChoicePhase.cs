namespace CardGames.Poker.Games;

/// <summary>
/// Phases of a Dealer's Choice game.
/// </summary>
public enum DealersChoicePhase
{
    /// <summary>Game has not started.</summary>
    WaitingToStart,

    /// <summary>Dealer is selecting the variant for this hand.</summary>
    SelectingVariant,

    /// <summary>Dealer is configuring wild card options (if enabled).</summary>
    ConfiguringWildCards,

    /// <summary>Variant-specific game is in progress.</summary>
    PlayingHand,

    /// <summary>Hand has completed.</summary>
    HandComplete
}
