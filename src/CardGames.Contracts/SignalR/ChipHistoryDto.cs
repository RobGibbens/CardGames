namespace CardGames.Contracts.SignalR;

/// <summary>
/// Represents a single entry in the chip history tracking.
/// </summary>
public sealed record ChipHistoryEntryDto
{
	/// <summary>
	/// The 1-based hand number within the game.
	/// </summary>
	public required int HandNumber { get; init; }

	/// <summary>
	/// The player's chip stack at the end of this hand.
	/// </summary>
	public required int ChipStackAfterHand { get; init; }

	/// <summary>
	/// The net chip change for this hand (positive = won, negative = lost).
	/// </summary>
	public required int ChipsDelta { get; init; }

	/// <summary>
	/// The UTC timestamp when the hand completed.
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Contains chip history and current chip state for a player.
/// </summary>
public sealed record ChipHistoryDto
{
	/// <summary>
	/// The player's current chip stack.
	/// </summary>
	public required int CurrentChips { get; init; }

	/// <summary>
	/// Chips pending to be added (queued until BetweenHands state for applicable game types).
	/// </summary>
	public int PendingChipsToAdd { get; init; }

	/// <summary>
	/// The player's starting chip stack for this game session.
	/// </summary>
	public required int StartingChips { get; init; }

	/// <summary>
	/// History of chip changes for the last 30 hands.
	/// Sorted chronologically (oldest to newest).
	/// </summary>
	public required IReadOnlyList<ChipHistoryEntryDto> History { get; init; }
}
