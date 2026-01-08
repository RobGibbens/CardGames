using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetTableSettings;

/// <summary>
/// Handles the GetTableSettingsQuery.
/// </summary>
public sealed class GetTableSettingsQueryHandler(CardsDbContext context)
    : IRequestHandler<GetTableSettingsQuery, GetTableSettingsResponse?>
{
    /// <inheritdoc />
    public async Task<GetTableSettingsResponse?> Handle(GetTableSettingsQuery query, CancellationToken cancellationToken)
    {
        var game = await context.Games
            .AsNoTracking()
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
            .FirstOrDefaultAsync(g => g.Id == query.GameId, cancellationToken);

        if (game is null)
        {
            return null;
        }

        var seatedPlayerCount = game.GamePlayers.Count(p => p.Status == Data.Entities.GamePlayerStatus.Active);

        return GetTableSettingsMapper.MapToResponse(game, seatedPlayerCount);
    }
}
