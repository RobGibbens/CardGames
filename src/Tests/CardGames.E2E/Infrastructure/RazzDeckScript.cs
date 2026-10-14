namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Razz deck in the order the server deals it.
///     Third street and later streets start left of the dealer and skip anyone who folded.
///     Each player receives two hole cards and a door on third street. Seventh street is face down.
/// </summary>
public static class RazzDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(int dealerSeat, RazzHandScript script) =>
		Trace(dealerSeat, script).Deck;

	public static RazzDealTrace Trace(int dealerSeat, RazzHandScript script)
	{
		if (script.Holdings.Count != RazzFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Razz needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[RazzFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(RazzFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding, script);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var street in Enum.GetValues<RazzStreet>())
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

		return new RazzDealTrace
		{
			Deck = deck,
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	private static void DealStreet(
		SeatPlan[] plans,
		RazzStreet street,
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

			if (street == RazzStreet.Third)
			{
				while (plan.Received.Count < expected)
					Give(plan, faceUp: plan.Received.Count >= 2, deck, used);
			}
			else if (plan.Received.Count < expected)
			{
				Give(plan, faceUp: street != RazzStreet.Seventh, deck, used);
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
		Enumerable.Range(1, RazzFiveMaxCatalog.SeatCount)
			.Select(offset => (dealerSeat + offset) % RazzFiveMaxCatalog.SeatCount)
			.ToArray();

	private static int ExpectedCards(RazzStreet street) => street switch
	{
		RazzStreet.Third => 3,
		RazzStreet.Fourth => 4,
		RazzStreet.Fifth => 5,
		RazzStreet.Sixth => 6,
		RazzStreet.Seventh => 7,
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
		private readonly RazzHandScript _script;

		private SeatPlan(RazzHandScript script)
		{
			_script = script;
		}

		public required HoldEmPosition Position { get; init; }

		public required Queue<string> Queue { get; init; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		public static SeatPlan Create(HoldEmPosition position, RazzHolding holding, RazzHandScript script)
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

		public bool StillIn(RazzStreet street) =>
			!_script.Actions.Any(action =>
				action.Actor == Position &&
				action.Kind == RazzActionKind.Fold &&
				action.Street < street);

		public RazzDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record RazzDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, RazzDealtCards> Seats { get; init; }
}

public sealed record RazzDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
