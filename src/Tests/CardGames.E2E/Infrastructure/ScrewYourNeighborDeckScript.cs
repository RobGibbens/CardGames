namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds one 52-card Screw Your Neighbor deck in deal order.
///     Each hand takes one card per seat, left of the dealer through the dealer.
///     A dealer who trades also takes the next undealt card. The remainder stays for later hands.
/// </summary>
public static class ScrewYourNeighborDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(
		IReadOnlyList<ScrewYourNeighborHandScript> hands,
		string followingCard)
	{
		var ordered = new List<string>(StandardDeck.Length);
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var hand in hands)
		{
			foreach (var card in hand.DealOrderCards)
				ordered.Add(TakeCard(card, used));

			if (hand.DealerTrades)
			{
				if (hand.DealerDeckCard is null)
					throw new ArgumentException($"Hand '{hand.Name}' trades with the deck but names no deck card.");

				ordered.Add(TakeCard(hand.DealerDeckCard, used));
			}
		}

		ordered.Add(TakeCard(followingCard, used));

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
