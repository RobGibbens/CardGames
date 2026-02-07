using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Handler for retrieving game rules by game identifier.
/// </summary>
public sealed class GetGameRulesByGameIdQueryHandler(CardsDbContext context)
    : IRequestHandler<GetGameRulesByGameIdQuery, GetGameRulesResponse?>
{
    public async Task<GetGameRulesResponse?> Handle(GetGameRulesByGameIdQuery request, CancellationToken cancellationToken)
    {
        var gameTypeCode = await context.Games
            .Where(g => g.Id == request.GameId && !g.IsDeleted)
            .Select(g => g.GameType.Code)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(gameTypeCode))
        {
            return null;
        }

        if (!PokerGameRulesRegistry.TryGet(gameTypeCode, out var rules) || rules is null)
        {
            return null;
        }

        return GameRulesMapper.ToResponse(rules);
    }
}
