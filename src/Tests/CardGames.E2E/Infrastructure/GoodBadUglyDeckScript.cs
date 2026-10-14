namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Good, Bad, and Ugly deck. Three table cards are dealt first,
///     then four hole cards to each seat from the left of the dealer.
/// </summary>
public static class GoodBadUglyDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(int dealerSeat, GoodBadUglyHandScript script, bool communityFirst)
	{
		if (script.Community.Count != 3)
			throw new ArgumentException("The Good, the Bad, and the Ugly needs three community cards.", nameof(script));

		if (script.HoleCards.Count != GoodBadUglyFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Every seat needs four hole cards.", nameof(script));

		var holesBySeat = script.HoleCards.ToDictionary(
			pair => HoldEmSeats.Index(GoodBadUglyFiveMaxCatalog.SeatCount, dealerSeat, pair.Key),
			pair => pair.Value);

		var totalSeats = GoodBadUglyFiveMaxCatalog.SeatCount;
		var dealOrder = holesBySeat.Keys
			.OrderBy(seat => (seat - dealerSeat - 1 + totalSeats) % totalSeats)
			.ToList();

		var ordered = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		var holeCards = new List<string>();
		foreach (var seat in dealOrder)
		{
			var cards = holesBySeat[seat];
			if (cards.Count != 4)
				throw new ArgumentException($"Seat {seat} must have exactly four hole cards.");

			holeCards.AddRange(cards);
		}

		var dealt = communityFirst
			? script.Community.Concat(holeCards)
			: holeCards.Concat(script.Community);
		foreach (var card in dealt)
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
