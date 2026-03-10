using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetGamePreferences;

public sealed record GetGamePreferencesQuery : IRequest<GamePreferencesDto>;
