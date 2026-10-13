namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Kings and Lows deck in deal order.
///     The button-started hand deals five rounds by ascending seat.
///     Hands started by continuous play deal five cards to each player, beginning
///     with the seat left of the dealer. The following cards are the draw stub,
///     then the deck hand when a player faces the deck.
/// </summary>
public static class KingsAndLowsDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(
		IReadOnlyDictionary<int, IReadOnlyList<string>> holeCardsBySeat,
		IReadOnlyList<string> drawStub,
		int dealerSeat = 0,
		bool roundRobinBySeat = false)
	{
		if (holeCardsBySeat.Count < 2)
			throw new ArgumentException("Kings and Lows needs at least two seated players.", nameof(holeCardsBySeat));

		if (!holeCardsBySeat.ContainsKey(dealerSeat))
			throw new ArgumentException("The dealer seat must be occupied.", nameof(dealerSeat));

		foreach (var (seat, cards) in holeCardsBySeat)
		{
			if (cards.Count != 5)
				throw new ArgumentException($"Seat {seat} must have exactly five hole cards.");
		}

		var seats = holeCardsBySeat.Keys.ToList();
		var totalSeats = seats.Max() + 1;
		var ordered = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		if (roundRobinBySeat)
		{
			var bySeat = seats.OrderBy(seat => seat).ToList();
			for (var round = 0; round < 5; round++)
			{
				foreach (var seat in bySeat)
					ordered.Add(TakeCard(holeCardsBySeat[seat][round], used));
			}
		}
		else
		{
			var byPlayer = seats
				.OrderBy(seat => (seat - dealerSeat - 1 + totalSeats) % totalSeats)
				.ToList();
			foreach (var seat in byPlayer)
			{
				foreach (var card in holeCardsBySeat[seat])
					ordered.Add(TakeCard(card, used));
			}
		}

		foreach (var card in drawStub)
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
