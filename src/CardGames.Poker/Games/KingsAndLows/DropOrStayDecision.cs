namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Represents a player's decision in the drop-or-stay phase.
/// </summary>
public enum DropOrStayDecision
{
	/// <summary>Player has not yet decided.</summary>
	Undecided,

	/// <summary>Player chooses to drop (fold) and not participate in this hand.</summary>
	Drop,

	/// <summary>Player chooses to stay and continue to draw/showdown.</summary>
	Stay
}