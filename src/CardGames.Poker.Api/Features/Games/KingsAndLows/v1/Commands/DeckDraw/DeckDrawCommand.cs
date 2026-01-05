using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

/// <summary>
/// Command to process the deck's draw in the player-vs-deck scenario of Kings and Lows.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="PlayerId">The player (dealer or designated) choosing the deck's discards.</param>
/// <param name="DiscardIndices">Zero-based indices (0-4) of the cards to discard from the deck's hand.
/// Pass an empty collection to keep all cards.</param>
public record DeckDrawCommand(
	Guid GameId,
	Guid PlayerId,
	IReadOnlyCollection<int> DiscardIndices
) : IRequest<OneOf<DeckDrawSuccessful, DeckDrawError>>, IGameStateChangingCommand;

/// <summary>
/// Request model for the deck draw endpoint.
/// </summary>
public record DeckDrawRequest
{
	/// <summary>
	/// The player making the decision for the deck.
	/// </summary>
	public required Guid PlayerId { get; init; }

	/// <summary>
	/// Zero-based indices (0-4) of the cards to discard from the deck's hand.
	/// </summary>
	public IReadOnlyCollection<int> DiscardIndices { get; init; } = [];
}
