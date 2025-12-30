using CardGames.Contracts.GameRules;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Response containing game rules metadata.
/// </summary>
public sealed record GetGameRulesResponse(GameRulesDto Rules);
