namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the type of deck used in a poker game.
/// </summary>
public enum DeckType
{
    /// <summary>
    /// Standard 52-card deck.
    /// </summary>
    Full52,

    /// <summary>
    /// Short deck (36 cards, 6-A only).
    /// </summary>
    Short36,

    /// <summary>
    /// Custom deck composition.
    /// </summary>
    Custom
}
