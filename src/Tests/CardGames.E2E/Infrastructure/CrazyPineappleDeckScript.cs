namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Crazy Pineapple deck in the order <c>DealDrawStyleCardsAsync</c> deals it.
///     Each player receives three hole cards before the next player, starting left of the dealer.
///     The next five cards are the flop, turn, and river. There is no burn card.
/// </summary>
public static class CrazyPineappleDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(
		int dealerSeat,
		IReadOnlyDictionary<int, IReadOnlyList<string>> holeCardsBySeat,
		IReadOnlyList<string> board)
	{
		if (holeCardsBySeat.Count < 2)
			throw new ArgumentException("Crazy Pineapple needs at least two seated players.", nameof(holeCardsBySeat));

		if (board.Count != 5)
			throw new ArgumentException("The board must contain five cards.", nameof(board));

		if (!holeCardsBySeat.ContainsKey(dealerSeat))
			throw new ArgumentException("The dealer seat must be occupied.", nameof(dealerSeat));

		foreach (var (seat, cards) in holeCardsBySeat)
		{
			if (cards.Count != 3)
				throw new ArgumentException($"Seat {seat} must have exactly three hole cards.");
		}

		var maxSeat = holeCardsBySeat.Keys.Max();
		var totalSeats = maxSeat + 1;
		var dealOrder = holeCardsBySeat.Keys
			.OrderBy(seat => (seat - dealerSeat - 1 + totalSeats) % totalSeats)
			.ToList();

		var ordered = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var seat in dealOrder)
		{
			foreach (var card in holeCardsBySeat[seat])
				ordered.Add(TakeCard(card, used));
		}

		foreach (var card in board)
			ordered.Add(TakeCard(card, used));

		foreach (var card in StandardDeck)
		{
			if (used.Add(card))
				ordered.Add(card);
		}

		if (ordered.Count != StandardDeck.Length)
			throw new ArgumentException("The script did not contain a valid 52-card deck.");

		return ordered;
	}

	private static string TakeCard(string card, HashSet<string> used)
	{
		var normalized = Normalize(card);
		if (!used.Add(normalized))
			throw new ArgumentException($"Card '{card}' is repeated.");

		return normalized;
	}

	private static string Normalize(string card)
	{
		if (string.IsNullOrWhiteSpace(card) || card.Trim().Length != 2)
			throw new ArgumentException($"'{card}' is not a valid card.");

		var text = card.Trim();
		var normalized = $"{char.ToUpperInvariant(text[0])}{char.ToLowerInvariant(text[1])}";
		if (!StandardDeck.Contains(normalized, StringComparer.Ordinal))
			throw new ArgumentException($"'{card}' is not a valid card.");

		return normalized;
	}
}
