namespace CardGames.Poker.Games.GameFlow;

/// <summary>
/// Represents a phase in a poker game's lifecycle.
/// Phases define the structure of gameplay and determine what actions are available.
/// </summary>
public interface IGamePhase
{
    /// <summary>
    /// Gets the unique identifier for this phase type.
    /// </summary>
    string PhaseId { get; }

    /// <summary>
    /// Gets the human-readable name of this phase.
    /// </summary>
    string PhaseName { get; }

    /// <summary>
    /// Gets a description of what happens in this phase.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category of this phase for UI grouping.
    /// Examples: "Setup", "Dealing", "Betting", "Drawing", "Resolution"
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Indicates whether this phase requires player action.
    /// </summary>
    bool RequiresPlayerAction { get; }

    /// <summary>
    /// Indicates whether this phase is a final/terminal phase.
    /// </summary>
    bool IsTerminal { get; }
}
