using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetFavoriteVariants;

public sealed record GetFavoriteVariantsQuery : IRequest<FavoriteVariantsDto>;