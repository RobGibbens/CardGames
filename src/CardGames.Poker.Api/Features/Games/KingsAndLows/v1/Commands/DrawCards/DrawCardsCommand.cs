using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

/// <summary>
/// Command to process a draw action from a player in a Kings and Lows game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The unique identifier of the player drawing cards.</param>
/// <param name="DiscardIndices">Zero-based indices (0-4) of the cards to discard from the player's hand.
/// Pass an empty collection to "stand pat" (keep all cards). In Kings and Lows, up to 5 cards can be discarded.</param>
public record DrawCardsCommand(
	Guid GameId,
	Guid PlayerId,
	IReadOnlyCollection<int> DiscardIndices
) : IRequest<OneOf<DrawCardsSuccessful, DrawCardsError>>, IGameStateChangingCommand;

/// <summary>
/// Request model for the draw action endpoint.
/// </summary>
public record DrawCardsRequest
{
	/// <summary>
	/// The player making the draw.
	/// </summary>
	public required Guid PlayerId { get; init; }

	/// <summary>
	/// Zero-based indices (0-4) of the cards to discard from the player's hand.
	/// Pass an empty array to "stand pat" (keep all cards). In Kings and Lows, up to 5 cards can be discarded.
	/// </summary>
	public IReadOnlyCollection<int> DiscardIndices { get; init; } = [];
}
