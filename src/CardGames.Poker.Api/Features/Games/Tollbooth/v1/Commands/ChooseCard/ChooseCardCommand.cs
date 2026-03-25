using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public record ChooseCardCommand(
	Guid GameId,
	TollboothChoice Choice,
	int? PlayerSeatIndex = null) : IRequest<OneOf<ChooseCardSuccessful, ChooseCardError>>, IGameStateChangingCommand;

public record ChooseCardRequest
{
	/// <summary>
	/// The Tollbooth choice: Furthest (free), Nearest (1× ante), or Deck (2× ante).
	/// </summary>
	public TollboothChoice Choice { get; init; }

	/// <summary>
	/// Optional: explicit seat index override (for testing / admin).
	/// </summary>
	public int? PlayerSeatIndex { get; init; }
}

/// <summary>
/// The three Tollbooth card choices available to each player.
/// </summary>
public enum TollboothChoice
{
	/// <summary>Take the card furthest from the deck (free).</summary>
	Furthest = 0,

	/// <summary>Take the card nearest the deck (costs 1× ante).</summary>
	Nearest = 1,

	/// <summary>Take the top face-down card from the deck (costs 2× ante).</summary>
	Deck = 2
}
