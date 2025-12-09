using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;

/// <summary>
/// Command to collect antes from all players in a Five Card Draw game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to collect antes from.</param>
public record CollectAntesCommand(Guid GameId) : IRequest<OneOf<CollectAntesSuccessful, CollectAntesError>>;
