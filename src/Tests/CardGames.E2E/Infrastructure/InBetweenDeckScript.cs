namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card In-Between deck in deal order.
///     Each turn consumes its boundary cards, then its third card when the player bets.
///     A pass consumes only the two boundary cards. There is no burn card.
/// </summary>
public static class InBetweenDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(IReadOnlyList<string> dealtInOrder)
	{
		var ordered = new List<string>(StandardDeck.Length);
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var card in dealtInOrder)
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
