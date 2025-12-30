using CardGames.Poker.Api.Games;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Handler for retrieving game rules.
/// </summary>
public sealed class GetGameRulesQueryHandler : IRequestHandler<GetGameRulesQuery, GetGameRulesResponse?>
{
    public Task<GetGameRulesResponse?> Handle(GetGameRulesQuery request, CancellationToken cancellationToken)
    {
        if (!PokerGameRulesRegistry.TryGet(request.GameTypeCode, out var rules) || rules is null)
        {
            return Task.FromResult<GetGameRulesResponse?>(null);
        }

        var response = GameRulesMapper.ToResponse(rules);
        return Task.FromResult<GetGameRulesResponse?>(response);
    }
}
