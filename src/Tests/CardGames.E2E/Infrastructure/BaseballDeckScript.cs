namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Baseball deck in the order the server deals it.
///     Every street starts left of the dealer. A face-up four pauses the street.
///     Accepting the buy takes the next deck card as a face-down extra, and dealing
///     then resumes left of the dealer, skipping anyone who already has enough cards for the street.
/// </summary>
public static class BaseballDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static IReadOnlyList<string> Build(int dealerSeat, BaseballHandScript script) =>
		Trace(dealerSeat, script).Deck;

	public static BaseballDealTrace Trace(int dealerSeat, BaseballHandScript script)
	{
		if (script.Holdings.Count != BaseballFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Baseball needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[BaseballFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(BaseballFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding, script);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var street in Enum.GetValues<BaseballStreet>())
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

		return new BaseballDealTrace
		{
			Deck = deck,
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	private static void DealStreet(
		SeatPlan[] plans,
		BaseballStreet street,
		List<string> deck,
		HashSet<string> used,
		int dealerSeat)
	{
		var expected = ExpectedCards(street);
		var order = LeftOfDealer(dealerSeat);

		while (true)
		{
			var paused = false;
			foreach (var seat in order)
			{
				var plan = plans[seat]!;
				if (!plan.StillIn(street) || plan.Received.Count >= expected)
					continue;

				if (street == BaseballStreet.Third)
				{
					while (plan.Received.Count < expected)
					{
						var faceUp = plan.Received.Count >= 2;
						if (Give(plan, faceUp, deck, used))
						{
							paused = true;
							break;
						}
					}
				}
				else
				{
					paused = Give(plan, faceUp: street != BaseballStreet.Seventh, deck, used);
				}

				if (paused)
					break;
			}

			if (!paused)
				return;
		}
	}

	private static bool Give(SeatPlan plan, bool faceUp, List<string> deck, HashSet<string> used)
	{
		if (plan.Queue.Count == 0)
			throw new InvalidOperationException($"Seat {plan.Position} ran out of scripted cards.");

		var card = plan.Queue.Dequeue();
		Take(card, deck, used);
		plan.Received.Add(card);
		if (faceUp)
			plan.Up.Add(card);

		if (!faceUp || !IsFour(card))
			return false;

		if (plan.BuyCard is not null)
		{
			Take(plan.BuyCard, deck, used);
			plan.Received.Add(plan.BuyCard);
			plan.BuyCard = null;
		}

		return true;
	}

	private static void Take(string card, List<string> deck, HashSet<string> used)
	{
		var normalized = Normalize(card);
		if (!used.Add(normalized))
			throw new ArgumentException($"Card '{card}' is repeated.");

		deck.Add(normalized);
	}

	private static IReadOnlyList<int> LeftOfDealer(int dealerSeat) =>
		Enumerable.Range(1, BaseballFiveMaxCatalog.SeatCount)
			.Select(offset => (dealerSeat + offset) % BaseballFiveMaxCatalog.SeatCount)
			.ToArray();

	private static int ExpectedCards(BaseballStreet street) => street switch
	{
		BaseballStreet.Third => 3,
		BaseballStreet.Fourth => 4,
		BaseballStreet.Fifth => 5,
		BaseballStreet.Sixth => 6,
		BaseballStreet.Seventh => 7,
		_ => throw new ArgumentOutOfRangeException(nameof(street), street, "Unknown street.")
	};

	private static bool IsFour(string card) =>
		card.Trim().StartsWith('4');

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
		public required HoldEmPosition Position { get; init; }

		public required Queue<string> Queue { get; init; }

		public string? BuyCard { get; set; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		private readonly BaseballHandScript _script;

		private SeatPlan(BaseballHandScript script)
		{
			_script = script;
		}

		public static SeatPlan Create(HoldEmPosition position, BaseballHolding holding, BaseballHandScript script)
		{
			var cards = new List<string> { holding.FirstHole, holding.SecondHole, holding.Door };
			if (holding.Fourth is not null) cards.Add(holding.Fourth);
			if (holding.Fifth is not null) cards.Add(holding.Fifth);
			if (holding.Sixth is not null) cards.Add(holding.Sixth);
			if (holding.Seventh is not null) cards.Add(holding.Seventh);

			var faceUpFours = new[] { holding.Door, holding.Fourth, holding.Fifth, holding.Sixth }
				.Count(card => card is not null && IsFour(card));
			if (holding.BuyCard is not null && faceUpFours != 1)
				throw new ArgumentException($"{position} can buy exactly one face-up four.");

			return new SeatPlan(script)
			{
				Position = position,
				Queue = new Queue<string>(cards),
				BuyCard = holding.BuyCard
			};
		}

		public bool StillIn(BaseballStreet street) =>
			!_script.Actions.Any(action =>
				action.Actor == Position &&
				action.Kind == BaseballActionKind.Fold &&
				action.Street < street);

		public BaseballDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record BaseballDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, BaseballDealtCards> Seats { get; init; }
}

public sealed record BaseballDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
