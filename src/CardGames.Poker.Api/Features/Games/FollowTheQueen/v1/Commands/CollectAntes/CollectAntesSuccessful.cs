namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.CollectAntes;

/// <summary>
/// Represents a successful collect antes operation.
/// </summary>
public record CollectAntesSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The total amount of antes collected.
	/// </summary>
	public required int TotalAntesCollected { get; init; }

	/// <summary>
	/// The current phase of the game.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// List of ante contributions from each player.
	/// </summary>
	public required List<AnteContribution> AnteContributions { get; init; }
}

/// <summary>
/// Represents an ante contribution from a player.
/// </summary>
public record AnteContribution
{
	/// <summary>
	/// The name of the player who made the contribution.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The amount contributed.
	/// </summary>
	public required int Amount { get; init; }

	/// <summary>
	/// The player's remaining chip stack after the contribution.
	/// </summary>
	public required int RemainingChips { get; init; }

	/// <summary>
	/// Whether the player went all-in with this contribution.
	/// </summary>
	public required bool WentAllIn { get; init; }
}
