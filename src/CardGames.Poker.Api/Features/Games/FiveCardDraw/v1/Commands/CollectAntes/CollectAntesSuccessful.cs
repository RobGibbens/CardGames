namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;

/// <summary>
/// Represents a successful collection of antes from all players.
/// </summary>
public record CollectAntesSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// The total amount collected from all antes.
	/// </summary>
	public int TotalAntesCollected { get; init; }

	/// <summary>
	/// The current phase of the game after collecting antes.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The individual ante contributions from each player.
	/// </summary>
	public required IReadOnlyList<AnteContribution> AnteContributions { get; init; }
}

/// <summary>
/// Represents an individual player's ante contribution.
/// </summary>
public record AnteContribution
{
	/// <summary>
	/// The name of the player who contributed the ante.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The amount contributed by this player.
	/// </summary>
	/// <remarks>
	/// This may be less than the game's ante amount if the player was short-stacked.
	/// </remarks>
	public int Amount { get; init; }

	/// <summary>
	/// The player's chip stack after contributing the ante.
	/// </summary>
	public int RemainingChips { get; init; }

	/// <summary>
	/// Indicates whether the player went all-in with this ante.
	/// </summary>
	public bool WentAllIn { get; init; }
}

/// <summary>
/// Represents an error when collecting antes.
/// </summary>
public record CollectAntesError
{
	/// <summary>
	/// The error message describing why antes could not be collected.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required CollectAntesErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for collect antes failures.
/// </summary>
public enum CollectAntesErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in a valid state to collect antes.
	/// </summary>
	InvalidGameState
}
