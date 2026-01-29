namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;

/// <summary>
/// Successful result of resuming after chip check.
/// </summary>
public class ResumeAfterChipCheckSuccessful
{
	/// <summary>
	/// The game ID.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// A message describing the outcome.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The current phase after resuming.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The number of players who will auto-drop due to insufficient chips.
	/// </summary>
	public int PlayersAutoDropping { get; init; }
}
