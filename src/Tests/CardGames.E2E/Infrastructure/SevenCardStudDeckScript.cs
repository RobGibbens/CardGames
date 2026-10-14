namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Seven Card Stud deck in the order the server deals it.
///     Every street starts at the seat left of the dealer and skips anyone who already folded.
///     Third street gives each player two down cards and one up card before the next player.
///     Fourth, fifth, and sixth street are one up card. Seventh street is one down card.
/// </summary>
public static class SevenCardStudDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(int dealerSeat, SevenCardStudHandScript script) =>
		Trace(dealerSeat, script).Deck;

	public static SevenCardStudDealTrace Trace(int dealerSeat, SevenCardStudHandScript script)
	{
		if (script.Holdings.Count != SevenCardStudFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Seven Card Stud needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[SevenCardStudFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(SevenCardStudFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding, script);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var street in Enum.GetValues<SevenCardStudStreet>())
		{
			var stillIn = plans.Count(plan => plan!.StillIn(street));
			if (stillIn < 2)
				break;

			DealStreet(plans, street, deck, used, dealerSeat);
		}

		foreach (var card in StandardDeck)
		{
			if (used.Add(card))
				deck.Add(card);
		}

		if (deck.Count != StandardDeck.Length)
			throw new ArgumentException("The script did not contain a valid 52-card deck.");

		return new SevenCardStudDealTrace
		{
			Deck = deck,
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	private static void DealStreet(
		SeatPlan[] plans,
		SevenCardStudStreet street,
		List<string> deck,
		HashSet<string> used,
		int dealerSeat)
	{
		var expected = ExpectedCards(street);
		foreach (var seat in LeftOfDealer(dealerSeat))
		{
			var plan = plans[seat]!;
			if (!plan.StillIn(street))
				continue;

			if (street == SevenCardStudStreet.Third)
			{
				while (plan.Received.Count < expected)
					Give(plan, faceUp: plan.Received.Count >= 2, deck, used);
			}
			else if (plan.Received.Count < expected)
			{
				Give(plan, faceUp: street != SevenCardStudStreet.Seventh, deck, used);
			}
		}
	}

	private static void Give(SeatPlan plan, bool faceUp, List<string> deck, HashSet<string> used)
	{
		if (plan.Queue.Count == 0)
			throw new InvalidOperationException($"Seat {plan.Position} ran out of scripted cards.");

		var card = plan.Queue.Dequeue();
		Take(card, deck, used);
		plan.Received.Add(card);
		if (faceUp)
			plan.Up.Add(card);
	}

	private static void Take(string card, List<string> deck, HashSet<string> used)
	{
		var normalized = Normalize(card);
		if (!used.Add(normalized))
			throw new ArgumentException($"Card '{card}' is repeated.");

		deck.Add(normalized);
	}

	private static IReadOnlyList<int> LeftOfDealer(int dealerSeat) =>
		Enumerable.Range(1, SevenCardStudFiveMaxCatalog.SeatCount)
			.Select(offset => (dealerSeat + offset) % SevenCardStudFiveMaxCatalog.SeatCount)
			.ToArray();

	private static int ExpectedCards(SevenCardStudStreet street) => street switch
	{
		SevenCardStudStreet.Third => 3,
		SevenCardStudStreet.Fourth => 4,
		SevenCardStudStreet.Fifth => 5,
		SevenCardStudStreet.Sixth => 6,
		SevenCardStudStreet.Seventh => 7,
		_ => throw new ArgumentOutOfRangeException(nameof(street), street, "Unknown street.")
	};

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

	private sealed class SeatPlan
	{
		private readonly SevenCardStudHandScript _script;

		private SeatPlan(SevenCardStudHandScript script)
		{
			_script = script;
		}

		public required HoldEmPosition Position { get; init; }

		public required Queue<string> Queue { get; init; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		public static SeatPlan Create(
			HoldEmPosition position,
			SevenCardStudHolding holding,
			SevenCardStudHandScript script)
		{
			var cards = new List<string> { holding.FirstHole, holding.SecondHole, holding.Door };
			if (holding.Fourth is not null) cards.Add(holding.Fourth);
			if (holding.Fifth is not null) cards.Add(holding.Fifth);
			if (holding.Sixth is not null) cards.Add(holding.Sixth);
			if (holding.Seventh is not null) cards.Add(holding.Seventh);

			return new SeatPlan(script)
			{
				Position = position,
				Queue = new Queue<string>(cards)
			};
		}

		public bool StillIn(SevenCardStudStreet street) =>
			!_script.Actions.Any(action =>
				action.Actor == Position &&
				action.Kind == SevenCardStudActionKind.Fold &&
				action.Street < street);

		public SevenCardStudDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record SevenCardStudDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, SevenCardStudDealtCards> Seats { get; init; }
}

public sealed record SevenCardStudDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
