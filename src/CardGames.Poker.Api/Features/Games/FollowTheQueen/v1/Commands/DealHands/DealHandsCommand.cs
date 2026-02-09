using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;

/// <summary>
/// Command to deal cards for the current street in a Follow the Queen game.
/// Third Street: 2 hole cards + 1 board card. Subsequent streets: 1 card per street.
/// </summary>
/// <param name="GameId">The unique identifier of the game to deal cards in.</param>
public record DealHandsCommand(Guid GameId) : IRequest<OneOf<DealHandsSuccessful, DealHandsError>>, IGameStateChangingCommand;
