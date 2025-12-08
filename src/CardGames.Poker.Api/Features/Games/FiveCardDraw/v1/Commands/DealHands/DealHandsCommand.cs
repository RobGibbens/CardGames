using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;

/// <summary>
/// Command to deal five cards to each active player in a Five Card Draw game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to deal cards in.</param>
public record DealHandsCommand(Guid GameId) : IRequest<OneOf<DealHandsSuccessful, DealHandsError>>;
