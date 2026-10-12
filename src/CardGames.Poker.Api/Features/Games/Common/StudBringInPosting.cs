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

	/// <summary>
	///     Chooses the first voluntary actor after a third-street bring-in.
	///     A posted bring-in has already acted. The next live seat to the left acts,
	///     and the poster acts later only if someone raises.
	/// </summary>
	public static BringInFirstActor FirstActorAfterBringIn(
		IEnumerable<GamePlayer> players,
		int bringInSeat,
		int amountPosted)
	{
		if (amountPosted <= 0)
			return new BringInFirstActor(bringInSeat, -1, 0, false);

		var seated = players as IReadOnlyList<GamePlayer> ?? players.ToList();
		var bringInPlayer = seated.FirstOrDefault(player => player.SeatPosition == bringInSeat);
		var playersActed = bringInPlayer is not null && !bringInPlayer.IsAllIn ? 1 : 0;

		if (seated.Count == 0)
			return new BringInFirstActor(-1, bringInSeat, playersActed, true);

		var totalSeats = seated.Max(player => player.SeatPosition) + 1;
		var seat = (bringInSeat + 1) % totalSeats;

		for (var step = 0; step < totalSeats; step++)
		{
			var candidate = seated.FirstOrDefault(player => player.SeatPosition == seat);
			if (candidate is not null &&
			    !candidate.HasFolded &&
			    !candidate.IsAllIn &&
			    candidate.SeatPosition != bringInSeat)
				return new BringInFirstActor(seat, bringInSeat, playersActed, false);

			seat = (seat + 1) % totalSeats;
		}

		return new BringInFirstActor(-1, bringInSeat, playersActed, true);
	}

	internal readonly record struct BringInFirstActor(
		int ActorSeat,
		int LastAggressorIndex,
		int PlayersActed,
		bool RoundComplete);
}
