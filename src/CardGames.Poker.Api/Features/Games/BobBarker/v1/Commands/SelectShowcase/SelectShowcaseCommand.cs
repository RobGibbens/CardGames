using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public record SelectShowcaseCommand(
    Guid GameId,
    int ShowcaseCardIndex,
    int? PlayerSeatIndex = null) : IRequest<OneOf<SelectShowcaseSuccessful, SelectShowcaseError>>, IGameStateChangingCommand;

public record SelectShowcaseRequest
{
    public int ShowcaseCardIndex { get; init; }

    public int? PlayerSeatIndex { get; init; }
}