namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Result when a player successfully leaves a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player who left.</param>
/// <param name="PlayerName">The name of the player who left.</param>
/// <param name="LeftAtHandNumber">The hand number when the player left (-1 if not yet left).</param>
/// <param name="LeftAt">The timestamp when the player left (null if queued).</param>
/// <param name="FinalChipCount">The final chip count when leaving (null if queued).</param>
/// <param name="Immediate">True if player left immediately, false if queued for end of hand.</param>
/// <param name="Message">Optional message (e.g., for queued leaves).</param>
public sealed record LeaveGameSuccessful(
	Guid GameId,
	Guid PlayerId,
	string PlayerName,
	int LeftAtHandNumber,
	DateTimeOffset? LeftAt,
	int? FinalChipCount,
	bool Immediate,
	string? Message = null);
