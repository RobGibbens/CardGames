using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Query for retrieving game rules.
/// </summary>
public sealed record GetGameRulesQuery(string GameTypeCode) : IRequest<GetGameRulesResponse?>;
