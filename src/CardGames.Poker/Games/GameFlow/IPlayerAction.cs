namespace CardGames.Poker.Games.GameFlow;

/// <summary>
/// Represents an action that a player can take during a poker game.
/// </summary>
public interface IPlayerAction
{
    /// <summary>
    /// Gets the unique identifier for this action type.
    /// </summary>
    string ActionId { get; }

    /// <summary>
    /// Gets the human-readable name of this action.
    /// </summary>
    string ActionName { get; }

    /// <summary>
    /// Gets a description of what this action does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category of this action for UI grouping.
    /// Examples: "Betting", "Drawing", "Decision"
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Indicates whether this action requires a numeric parameter (e.g., bet amount, discard count).
    /// </summary>
    bool RequiresAmount { get; }

    /// <summary>
    /// Indicates whether this action requires selecting cards.
    /// </summary>
    bool RequiresCardSelection { get; }

    /// <summary>
    /// Gets the minimum value for the amount parameter, if applicable.
    /// </summary>
    int? MinAmount { get; }

    /// <summary>
    /// Gets the maximum value for the amount parameter, if applicable.
    /// </summary>
    int? MaxAmount { get; }
}
