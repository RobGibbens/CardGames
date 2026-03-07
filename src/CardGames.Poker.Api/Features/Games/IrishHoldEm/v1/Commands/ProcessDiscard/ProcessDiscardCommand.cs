using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Command to process a discard action from a player in an Irish Hold 'Em game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="DiscardIndices">Zero-based indices (0-3) of exactly 2 cards to discard from the player's hand.</param>
/// <param name="PlayerSeatIndex">
/// Optional seat index of the player performing the discard.
/// When omitted, falls back to CurrentDrawPlayerIndex for backward compatibility.
/// </param>
public record ProcessDiscardCommand(
	Guid GameId,
	IReadOnlyCollection<int> DiscardIndices,
	int? PlayerSeatIndex = null
) : IRequest<OneOf<ProcessDiscardSuccessful, ProcessDiscardError>>, IGameStateChangingCommand;

/// <summary>
/// Request model for the discard action endpoint.
/// </summary>
public record ProcessDiscardRequest
{
	/// <summary>
	/// Zero-based indices (0-3) of exactly 2 cards to discard from the player's hand.
	/// </summary>
	public IReadOnlyCollection<int> DiscardIndices { get; init; } = [];

	/// <summary>
	/// The seat index of the player performing the discard.
	/// </summary>
	public int? PlayerSeatIndex { get; init; }
}
