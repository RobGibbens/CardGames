using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Query for retrieving game rules by game identifier.
/// </summary>
public sealed record GetGameRulesByGameIdQuery(Guid GameId) : IRequest<GetGameRulesResponse?>;
