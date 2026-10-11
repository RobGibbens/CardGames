using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common;

/// <summary>
///     Posts the forced Third Street bring-in into the main pot.
/// </summary>
internal static class StudBringInPosting
{
	public static async Task<int> PostBringInForThirdStreetAsync(
		CardsDbContext context,
		Game game,
		GamePlayer bringInPlayer,
		int bringInAmount,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		if (bringInAmount <= 0 || bringInPlayer.ChipStack <= 0)
			return 0;

		var actualBringIn = Math.Min(bringInAmount, bringInPlayer.ChipStack);
		bringInPlayer.ChipStack -= actualBringIn;
		bringInPlayer.CurrentBet = actualBringIn;
		bringInPlayer.TotalContributedThisHand += actualBringIn;
		if (bringInPlayer.ChipStack == 0)
			bringInPlayer.IsAllIn = true;

		var pots = await context.Pots
			.Where(p => p.GameId == game.Id &&
			            p.HandNumber == game.CurrentHandNumber &&
			            p.PotType == PotType.Main)
			.ToListAsync(cancellationToken);

		// CreateGame and StartHand can both insert a main pot for hand 1.
		// Antes are on the pot that already has chips.
		var pot = pots
			.OrderByDescending(p => p.Amount)
			.ThenByDescending(p => p.CreatedAt)
			.FirstOrDefault();

		if (pot is not null)
		{
			pot.Amount += actualBringIn;

			var contribution = await context.PotContributions
				.FirstOrDefaultAsync(c => c.PotId == pot.Id && c.GamePlayerId == bringInPlayer.Id,
					cancellationToken);

			if (contribution is null)
			{
				context.PotContributions.Add(new PotContribution
				{
					PotId = pot.Id,
					GamePlayerId = bringInPlayer.Id,
					Amount = actualBringIn,
					IsEligibleToWin = true,
					IsPotMatch = false,
					ContributedAt = now
				});
			}
			else
			{
				contribution.Amount += actualBringIn;
				contribution.IsEligibleToWin = true;
			}
		}

		game.BringInPlayerIndex = bringInPlayer.SeatPosition;
		return actualBringIn;
	}
}
