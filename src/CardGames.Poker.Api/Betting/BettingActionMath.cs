namespace CardGames.Poker.Api.Betting;

/// <summary>
///     Shared raise limits for API betting handlers. <c>amount</c> is the player's total wager on the street.
/// </summary>
internal static class BettingActionMath
{
	public static int GetMaxRaiseTo(int playerCurrentBet, int chipStack)
	{
		return playerCurrentBet + chipStack;
	}

	public static int GetMinRaiseTo(int streetCurrentBet, int lastRaiseAmount)
	{
		return streetCurrentBet + lastRaiseAmount;
	}

	/// <summary>Returns null if valid; otherwise an error message for InvalidAction.</summary>
	public static string? ValidateRaise(
		int amount,
		int streetCurrentBet,
		int lastRaiseAmount,
		int playerCurrentBet,
		int chipStack)
	{
		var maxRaiseTo = GetMaxRaiseTo(playerCurrentBet, chipStack);
		if (amount > maxRaiseTo)
			return $"Cannot raise more than your stack ({chipStack})";

		if (amount <= streetCurrentBet)
			return $"Cannot raise to {amount} - must raise above the current bet of {streetCurrentBet}";

		var minRaiseTo = GetMinRaiseTo(streetCurrentBet, lastRaiseAmount);
		if (amount < minRaiseTo && amount < maxRaiseTo)
			return $"Raise must be to at least {minRaiseTo}";

		return null;
	}

	/// <summary>Computes chips moved and the new player total bet for a validated raise.</summary>
	public static (int raiseTotal, int chipsMoved, int raiseIncrement) ComputeRaise(
		int amount,
		int streetCurrentBet,
		int playerCurrentBet,
		int chipStack)
	{
		var maxRaiseTo = GetMaxRaiseTo(playerCurrentBet, chipStack);
		var raiseTotal = Math.Min(amount, maxRaiseTo);
		var chipsMoved = raiseTotal - playerCurrentBet;
		if (chipsMoved > chipStack)
			chipsMoved = chipStack;
		if (chipsMoved < 0)
			chipsMoved = 0;

		raiseTotal = playerCurrentBet + chipsMoved;
		var raiseIncrement = raiseTotal - streetCurrentBet;
		return (raiseTotal, chipsMoved, raiseIncrement);
	}
}
