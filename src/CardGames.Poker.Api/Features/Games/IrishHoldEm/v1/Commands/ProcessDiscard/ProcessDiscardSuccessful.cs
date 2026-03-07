using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Represents a successful discard action result for Irish Hold 'Em.
/// </summary>
public record ProcessDiscardSuccessful : IPlayerActionResult
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// The name of the player who performed the discard action.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The seat index of the player who performed the discard action.
	/// </summary>
	public int PlayerSeatIndex { get; init; }

	/// <summary>
	/// The cards that were discarded from the player's hand.
	/// </summary>
	public required IReadOnlyCollection<CardInfo> DiscardedCards { get; init; }

	/// <summary>
	/// Indicates whether all players have completed their discards.
	/// When true, the game advances to the Turn phase.
	/// </summary>
	public bool DiscardPhaseComplete { get; init; }

	/// <summary>
	/// The current phase of the game after the discard action.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The index of the next player to discard, or -1 if the discard phase is complete.
	/// </summary>
	public int NextDiscardPlayerIndex { get; init; }

	/// <summary>
	/// The name of the next player to discard, or null if the discard phase is complete.
	/// </summary>
	public string? NextDiscardPlayerName { get; init; }

	/// <inheritdoc />
	string? IPlayerActionResult.PlayerName => PlayerName;

	/// <inheritdoc />
	string IPlayerActionResult.ActionDescription => "Discarded 2";
}
