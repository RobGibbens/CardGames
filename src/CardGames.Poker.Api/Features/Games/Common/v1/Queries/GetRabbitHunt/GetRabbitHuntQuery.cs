using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

public sealed record GetRabbitHuntQuery(Guid GameId)
    : IRequest<OneOf<GetRabbitHuntSuccessful, GetRabbitHuntError>>;