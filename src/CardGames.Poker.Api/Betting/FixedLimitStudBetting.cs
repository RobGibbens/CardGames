namespace CardGames.Poker.Api.Betting;

/// <summary>
///     Fixed-limit stud totals. <c>MinBet</c> on the round is the street limit.
///     An aggressive action is that one total, or every remaining chip when the stack cannot reach it.
/// </summary>
internal static class FixedLimitStudBetting
{
	public static int LastFullLevel(int streetCurrentBet, int streetLimit)
	{
		if (streetLimit <= 0) return 0;
		if (streetCurrentBet < streetLimit) return 0;
		return streetCurrentBet / streetLimit * streetLimit;
	}

	public static int NextFullTotal(int streetCurrentBet, int streetLimit)
		=> LastFullLevel(streetCurrentBet, streetLimit) + streetLimit;

	public static int? LegalAggressiveTotal(
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int streetLimit)
	{
		if (chipStack <= 0 || streetLimit <= 0) return null;
		var maxTotal = playerCurrentBet + chipStack;
		if (maxTotal <= streetCurrentBet) return null;
		var nextFull = NextFullTotal(streetCurrentBet, streetLimit);
		return maxTotal >= nextFull ? nextFull : maxTotal;
	}

	public static bool IsFullLevel(int total, int streetLimit)
		=> streetLimit > 0 && total >= streetLimit && total % streetLimit == 0;

	public static string? OpeningBetError(int amount, int playerCurrentBet, int chipStack, int streetLimit)
	{
		var legal = LegalAggressiveTotal(0, playerCurrentBet, chipStack, streetLimit);
		if (streetLimit <= 0 || legal is not int total || amount != total)
			return legal is int required ? $"Bet must be {required}" : "Bet must be the street limit";

		return null;
	}

	public static string? RaiseError(
		int amount,
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int streetLimit)
	{
		var maxTotal = playerCurrentBet + chipStack;
		if (amount > maxTotal)
			return $"Cannot raise more than your stack ({chipStack})";

		var legal = LegalAggressiveTotal(streetCurrentBet, playerCurrentBet, chipStack, streetLimit);
		if (legal is not int total || amount != total)
			return legal is int required ? $"Raise must be to {required}" : "Raise must be to the street limit";

		return null;
	}

	/// <summary>
	///     Null when the all-in is a short call or the player's whole stack is the one legal total.
	/// </summary>
	public static string? RaisingAllInError(
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int streetLimit)
	{
		var newTotal = playerCurrentBet + chipStack;
		if (newTotal <= streetCurrentBet) return null;

		var legal = LegalAggressiveTotal(streetCurrentBet, playerCurrentBet, chipStack, streetLimit);
		if (legal is int total && newTotal == total) return null;

		return legal is int required
			? $"All-in must be the {required} bet"
			: "All-in must be the street bet";
	}

	public readonly record struct ActionOffer(
		bool CanBet,
		bool CanRaise,
		bool CanAllIn,
		int MinBet,
		int MaxBet,
		int MinRaise);

	public static ActionOffer? Offer(
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack,
		int streetLimit)
	{
		if (LegalAggressiveTotal(streetCurrentBet, playerCurrentBet, chipStack, streetLimit) is not int total)
			return null;

		return new ActionOffer(
			streetCurrentBet == 0,
			streetCurrentBet > 0,
			chipStack > 0 && playerCurrentBet + chipStack == total,
			streetCurrentBet == 0 ? total : streetLimit,
			total,
			total);
	}
}
