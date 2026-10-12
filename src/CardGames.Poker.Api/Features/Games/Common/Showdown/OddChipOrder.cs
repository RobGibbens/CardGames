namespace CardGames.Poker.Api.Features.Games.Common.Showdown;

/// <summary>
///     Orders tied winners clockwise from the dealer and gives each leftover chip to a different winner.
/// </summary>
public static class OddChipOrder
{
	public static List<T> OrderClockwiseFromDealer<T>(
		IEnumerable<T> winners,
		int dealerSeat,
		Func<T, int> seatSelector)
	{
		return winners
			.OrderBy(winner => seatSelector(winner) > dealerSeat ? 0 : 1)
			.ThenBy(winner => seatSelector(winner))
			.ToList();
	}

	public static List<int> Distribute<T>(int amount, IReadOnlyList<T> orderedWinners)
	{
		if (orderedWinners.Count == 0)
			return [];

		var share = amount / orderedWinners.Count;
		var remainder = amount % orderedWinners.Count;
		var payouts = new List<int>(orderedWinners.Count);
		for (var index = 0; index < orderedWinners.Count; index++)
		{
			var payout = share;
			if (remainder > 0)
			{
				payout++;
				remainder--;
			}

			payouts.Add(payout);
		}

		return payouts;
	}
}
