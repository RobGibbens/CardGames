using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

internal static class KnownDeadCards
{
	internal static Task<List<GameCard>> LoadAsync(
		CardsDbContext context,
		Guid gameId,
		int handNumber,
		CancellationToken cancellationToken)
	{
		return context.GameCards
			.Where(card => card.GamePlayer.GameId == gameId
			               && card.HandNumber == handNumber
			               && card.GamePlayer.HasFolded
			               && card.IsVisible
			               && !card.IsDiscarded)
			.AsNoTracking()
			.ToListAsync(cancellationToken);
	}
}
