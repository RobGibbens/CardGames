namespace CardGames.Contracts.SignalR;

/// <summary>
/// Represents a single player's result for a completed hand.
/// </summary>
public sealed record PlayerHandResultDto
{
	/// <summary>
	/// The unique identifier of the player.
	/// </summary>
	public required Guid PlayerId { get; init; }

	/// <summary>
	/// The display name of the player.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The seat position of the player during the hand.
	/// </summary>
	public required int SeatPosition { get; init; }

	/// <summary>
	/// The final result/action for this player (Won, Lost, SplitPot, Folded).
	/// </summary>
	public required string ResultType { get; init; }

	/// <summary>
	/// Human-readable result label (e.g., "Won", "Folded (Preflop)").
	/// </summary>
	public required string ResultLabel { get; init; }

	/// <summary>
	/// The net chip change for this player in the hand (positive = won, negative = lost).
	/// Includes antes, blinds, and all bets.
	/// </summary>
	public required int NetAmount { get; init; }

	/// <summary>
	/// Whether the player reached the showdown phase.
	/// </summary>
	public required bool ReachedShowdown { get; init; }

	/// <summary>
	/// The player's final visible hole cards (empty if folded or cards not shown).
	/// </summary>
	public IReadOnlyList<string>? VisibleCards { get; init; }
}

/// <summary>
/// Represents a completed hand entry for the dashboard history display.
/// </summary>
public sealed record HandHistoryEntryDto
{
	/// <summary>
	/// The 1-based hand number within the game.
	/// </summary>
	public required int HandNumber { get; init; }

	/// <summary>
	/// The UTC timestamp when the hand completed.
	/// </summary>
	public required DateTimeOffset CompletedAtUtc { get; init; }

	/// <summary>
	/// The display name of the winner (or first winner if split pot).
	/// </summary>
	public required string WinnerName { get; init; }

	/// <summary>
	/// The total amount in the pot that was won.
	/// </summary>
	public required int AmountWon { get; init; }

	/// <summary>
	/// Description of the winning hand (e.g., "Full House").
	/// Null for win-by-fold.
	/// </summary>
	public string? WinningHandDescription { get; init; }

	/// <summary>
	/// Whether the hand was won because all others folded.
	/// </summary>
	public bool WonByFold { get; init; }

	/// <summary>
	/// Number of players who won (for split pot display).
	/// </summary>
	public int WinnerCount { get; init; } = 1;

	/// <summary>
	/// Per-player results for all players who participated in this hand.
	/// </summary>
	public required IReadOnlyList<PlayerHandResultDto> PlayerResults { get; init; }
}

/// <summary>
/// Contains hand history for the dashboard flyout.
/// </summary>
public sealed record HandHistoryListDto
{
	/// <summary>
	/// The list of hand history entries, sorted newest-first.
	/// </summary>
	public required IReadOnlyList<HandHistoryEntryDto> Entries { get; init; }

	/// <summary>
	/// Total number of hands played in this game.
	/// </summary>
	public int TotalHands { get; init; }

	/// <summary>
	/// Whether there are more entries available beyond those returned.
	/// </summary>
	public bool HasMore { get; init; }
}
