namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Follow the Queen deck in the order the server deals it.
///     Every street starts left of the dealer and skips anyone who folded.
///     Seventh street is face down.
///     The wild-rank walk is not that deal order: each street is read clockwise from the
///     seat left of the dealer.
/// </summary>
public static class FollowTheQueenDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	private static readonly HoldEmPosition[] Walk =
	[
		HoldEmPosition.SmallBlind,
		HoldEmPosition.BigBlind,
		HoldEmPosition.UnderTheGun,
		HoldEmPosition.Cutoff,
		HoldEmPosition.Button
	];

	public static IReadOnlyList<string> Build(int dealerSeat, FollowTheQueenHandScript script) =>
		Trace(dealerSeat, script).Deck;

	public static FollowTheQueenDealTrace Trace(int dealerSeat, FollowTheQueenHandScript script)
	{
		if (script.Holdings.Count != FollowTheQueenFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Follow the Queen needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[FollowTheQueenFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(FollowTheQueenFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding, script);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var street in Enum.GetValues<FollowTheQueenStreet>())
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

		return new FollowTheQueenDealTrace
		{
			Deck = deck,
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	/// <summary>
	///     Face-up cards in the order that sets the follow rank: each street finished,
	///     then seats clockwise from the small blind. Folded players keep the up-cards
	///     they were dealt and receive none after they fold.
	/// </summary>
	public static IReadOnlyList<string> FaceUpWalk(FollowTheQueenHandScript script)
	{
		var walk = new List<string>();
		foreach (var street in Enum.GetValues<FollowTheQueenStreet>())
		{
			if (street == FollowTheQueenStreet.Seventh)
				break;

			foreach (var position in Walk)
			{
				if (!StillIn(script, position, street))
					continue;

				var card = UpCard(script.Holdings[position], street);
				if (card is not null)
					walk.Add(Normalize(card));
			}
		}

		return walk;
	}

	/// <summary>
	///     The rank label that follows the last face-up queen, or null when that queen
	///     is the last face-up card. Queens themselves are always wild and are not returned.
	/// </summary>
	public static string? FollowRank(IReadOnlyList<string> faceUpInOrder)
	{
		string? follow = null;
		for (var i = 0; i < faceUpInOrder.Count; i++)
		{
			if (!faceUpInOrder[i].StartsWith('Q'))
				continue;

			follow = i + 1 < faceUpInOrder.Count ? RankLabel(faceUpInOrder[i + 1]) : null;
		}

		return follow;
	}

	public static string RankLabel(string card)
	{
		return char.ToUpperInvariant(Normalize(card)[0]) switch
		{
			'A' => "Aces",
			'K' => "Kings",
			'Q' => "Queens",
			'J' => "Jacks",
			'T' => "Tens",
			'2' => "Deuces",
			'3' => "Threes",
			'4' => "Fours",
			'5' => "Fives",
			'6' => "Sixes",
			'7' => "Sevens",
			'8' => "Eights",
			'9' => "Nines",
			_ => throw new ArgumentException($"'{card}' is not a valid card.")
		};
	}

	private static void DealStreet(
		SeatPlan[] plans,
		FollowTheQueenStreet street,
		List<string> deck,
		HashSet<string> used,
		int dealerSeat)
	{
		var expected = ExpectedCards(street);
		var order = LeftOfDealer(dealerSeat);

		foreach (var seat in order)
		{
			var plan = plans[seat]!;
			if (!plan.StillIn(street))
				continue;

			if (street == FollowTheQueenStreet.Third)
			{
				while (plan.Received.Count < expected)
					Give(plan, faceUp: plan.Received.Count >= 2, deck, used);
			}
			else if (plan.Received.Count < expected)
			{
				Give(plan, faceUp: street != FollowTheQueenStreet.Seventh, deck, used);
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

	private static bool StillIn(FollowTheQueenHandScript script, HoldEmPosition position, FollowTheQueenStreet street) =>
		!script.Actions.Any(action =>
			action.Actor == position &&
			action.Kind == FollowTheQueenActionKind.Fold &&
			action.Street < street);

	private static string? UpCard(FollowTheQueenHolding holding, FollowTheQueenStreet street) => street switch
	{
		FollowTheQueenStreet.Third => holding.Door,
		FollowTheQueenStreet.Fourth => holding.Fourth,
		FollowTheQueenStreet.Fifth => holding.Fifth,
		FollowTheQueenStreet.Sixth => holding.Sixth,
		_ => null
	};

	private static IReadOnlyList<int> LeftOfDealer(int dealerSeat) =>
		Enumerable.Range(1, FollowTheQueenFiveMaxCatalog.SeatCount)
			.Select(offset => (dealerSeat + offset) % FollowTheQueenFiveMaxCatalog.SeatCount)
			.ToArray();

	private static int ExpectedCards(FollowTheQueenStreet street) => street switch
	{
		FollowTheQueenStreet.Third => 3,
		FollowTheQueenStreet.Fourth => 4,
		FollowTheQueenStreet.Fifth => 5,
		FollowTheQueenStreet.Sixth => 6,
		FollowTheQueenStreet.Seventh => 7,
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
		public required HoldEmPosition Position { get; init; }

		public required Queue<string> Queue { get; init; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		private readonly FollowTheQueenHandScript _script;

		private SeatPlan(FollowTheQueenHandScript script)
		{
			_script = script;
		}

		public static SeatPlan Create(
			HoldEmPosition position,
			FollowTheQueenHolding holding,
			FollowTheQueenHandScript script)
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

		public bool StillIn(FollowTheQueenStreet street) =>
			!_script.Actions.Any(action =>
				action.Actor == Position &&
				action.Kind == FollowTheQueenActionKind.Fold &&
				action.Street < street);

		public FollowTheQueenDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record FollowTheQueenDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, FollowTheQueenDealtCards> Seats { get; init; }
}

public sealed record FollowTheQueenDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
