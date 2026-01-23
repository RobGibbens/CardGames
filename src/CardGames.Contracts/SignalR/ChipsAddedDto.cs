namespace CardGames.Contracts.SignalR;

/// <summary>
/// Notification sent when chips are added or queued for a player.
/// </summary>
public sealed record ChipsAddedDto
{
	/// <summary>
	/// The game ID where chips were added.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The player name who received chips.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The amount of chips added.
	/// </summary>
	public required int Amount { get; init; }

	/// <summary>
	/// Whether chips were immediately added (true) or queued (false).
	/// </summary>
	public bool AppliedImmediately { get; init; }

	/// <summary>
	/// User-friendly message explaining when chips will be applied.
	/// </summary>
	public string? Message { get; init; }
}
