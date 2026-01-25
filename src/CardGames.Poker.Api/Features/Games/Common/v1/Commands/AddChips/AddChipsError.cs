namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Error codes for AddChips command failures.
/// </summary>
public enum AddChipsErrorCode
{
	/// <summary>
	/// The game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The player is not part of this game.
	/// </summary>
	PlayerNotInGame,

	/// <summary>
	/// The amount provided is invalid (e.g., zero or negative).
	/// </summary>
	InvalidAmount,

	/// <summary>
	/// Cannot add chips because the game has ended.
	/// </summary>
	GameEnded
}

/// <summary>
/// Error response for AddChips command.
/// </summary>
public sealed record AddChipsError(AddChipsErrorCode Code, string Message);
