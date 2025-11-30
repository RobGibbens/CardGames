namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the reveal status of a player's cards at showdown.
/// </summary>
public enum ShowdownRevealStatus
{
    /// <summary>
    /// Player has not yet decided whether to show or muck.
    /// </summary>
    Pending,

    /// <summary>
    /// Player chose to show their cards.
    /// </summary>
    Shown,

    /// <summary>
    /// Player chose to muck (not show) their cards.
    /// </summary>
    Mucked,

    /// <summary>
    /// Player was required to show due to rules (e.g., all-in, winner).
    /// </summary>
    ForcedReveal,

    /// <summary>
    /// Player folded and their cards remain hidden.
    /// </summary>
    Folded
}
