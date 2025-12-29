using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessDraw;

/// <summary>
/// Command to process a draw action from the current player in a Five Card Draw game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="DiscardIndices">Zero-based indices (0-4) of the cards to discard from the player's hand.
/// Pass an empty collection to "stand pat" (keep all cards). Maximum of 3 cards can be discarded,
/// or 4 cards if the player holds at least one Ace.</param>
public record ProcessDrawCommand(
	Guid GameId,
	IReadOnlyCollection<int> DiscardIndices
) : IRequest<OneOf<ProcessDrawSuccessful, ProcessDrawError>>, IGameStateChangingCommand;

/// <summary>
/// Request model for the draw action endpoint.
/// </summary>
public record ProcessDrawRequest
{
	/// <summary>
	/// Zero-based indices (0-4) of the cards to discard from the player's hand.
	/// Pass an empty array to "stand pat" (keep all cards). Maximum of 3 cards can be discarded,
	/// or 4 cards if the player holds at least one Ace.
	/// </summary>
	public IReadOnlyCollection<int> DiscardIndices { get; init; } = [];
}
