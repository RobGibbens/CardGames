using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Builds a 52-card Pair Pressure deck in the order the server deals it.
///     Every street, including the first hand, starts left of the dealer and skips folded players.
///     Seventh street is face down. Wild ranks follow the face-up walk: each street finished,
///     then seats clockwise from the seat left of the dealer.
/// </summary>
public static class PairPressureDeckScript
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

	public static IReadOnlyList<string> Build(int dealerSeat, PairPressureHandScript script) =>
		Trace(dealerSeat, script).Deck;

	public static PairPressureDealTrace Trace(int dealerSeat, PairPressureHandScript script)
	{
		if (script.Holdings.Count != PairPressureFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Pair Pressure needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[PairPressureFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(PairPressureFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding, script);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		foreach (var street in Enum.GetValues<PairPressureStreet>())
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

		return new PairPressureDealTrace
		{
			Deck = deck,
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	/// <summary>
	///     Face-up cards through <paramref name="through" />, clockwise from the small blind.
	///     Folded players keep the up-cards they were dealt and receive none after they fold.
	/// </summary>
	public static IReadOnlyList<string> FaceUpWalk(
		PairPressureHandScript script,
		PairPressureStreet through = PairPressureStreet.Sixth)
	{
		var walk = new List<string>();
		foreach (var street in Enum.GetValues<PairPressureStreet>())
		{
			if (street == PairPressureStreet.Seventh || street > through)
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
	///     Active wild-rank labels in queue order: the oldest of the two most recent distinct pairs first.
	/// </summary>
	public static IReadOnlyList<string> WildLabels(PairPressureHandScript script, PairPressureStreet through)
	{
		var faceUp = FaceUpWalk(script, through).Select(Parse).ToList();
		return new PairPressureWildCardRules()
			.DetermineWildRanks(faceUp)
			.Select(RankLabel)
			.ToArray();
	}

	/// <summary>
	///     The seat with the lowest door. Ace is high. Clubs, diamonds, hearts, then spades.
	/// </summary>
	public static HoldEmPosition BringIn(PairPressureHandScript script)
	{
		HoldEmPosition bringIn = default;
		string? lowest = null;
		foreach (var position in Walk)
		{
			var door = Normalize(script.Holdings[position].Door);
			if (lowest is null || CompareBringIn(door, lowest) < 0)
			{
				lowest = door;
				bringIn = position;
			}
		}

		return bringIn;
	}

	/// <summary>
	///     Third street acts after the bring-in. Later streets act from the best natural board.
	///     Wild ranks do not change that order.
	/// </summary>
	public static HoldEmPosition FirstActor(PairPressureHandScript script, PairPressureStreet street)
	{
		if (street == PairPressureStreet.Third)
		{
			var bringIn = BringIn(script);
			var index = Array.IndexOf(Walk, bringIn);
			return Walk[(index + 1) % Walk.Length];
		}

		HoldEmPosition? best = null;
		long bestStrength = -1;
		foreach (var position in Walk)
		{
			if (!StillIn(script, position, street))
				continue;

			var strength = VisibleStrength(script.Holdings[position], street);
			if (strength > bestStrength)
			{
				bestStrength = strength;
				best = position;
			}
		}

		return best ?? throw new InvalidOperationException($"No one is left to act on {street}.");
	}

	public static string RankLabel(int rank)
	{
		return rank switch
		{
			14 => "Aces",
			13 => "Kings",
			12 => "Queens",
			11 => "Jacks",
			10 => "Tens",
			2 => "Deuces",
			3 => "Threes",
			4 => "Fours",
			5 => "Fives",
			6 => "Sixes",
			7 => "Sevens",
			8 => "Eights",
			9 => "Nines",
			_ => throw new ArgumentException($"'{rank}' is not a rank.")
		};
	}

	public static Card Parse(string code)
	{
		var normalized = Normalize(code);
		var suit = normalized[1] switch
		{
			'h' => Suit.Hearts,
			'd' => Suit.Diamonds,
			's' => Suit.Spades,
			'c' => Suit.Clubs,
			_ => throw new ArgumentException(code)
		};
		var symbol = normalized[0] switch
		{
			'A' => Symbol.Ace,
			'K' => Symbol.King,
			'Q' => Symbol.Queen,
			'J' => Symbol.Jack,
			'T' => Symbol.Ten,
			'2' => Symbol.Deuce,
			'3' => Symbol.Three,
			'4' => Symbol.Four,
			'5' => Symbol.Five,
			'6' => Symbol.Six,
			'7' => Symbol.Seven,
			'8' => Symbol.Eight,
			'9' => Symbol.Nine,
			_ => throw new ArgumentException(code)
		};
		return new Card(suit, symbol);
	}

	private static void DealStreet(
		SeatPlan[] plans,
		PairPressureStreet street,
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

			if (street == PairPressureStreet.Third)
			{
				while (plan.Received.Count < expected)
					Give(plan, faceUp: plan.Received.Count >= 2, deck, used);
			}
			else if (plan.Received.Count < expected)
			{
				Give(plan, faceUp: street != PairPressureStreet.Seventh, deck, used);
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

	private static bool StillIn(PairPressureHandScript script, HoldEmPosition position, PairPressureStreet street) =>
		!script.Actions.Any(action =>
			action.Actor == position &&
			action.Kind == PairPressureActionKind.Fold &&
			action.Street < street);

	private static string? UpCard(PairPressureHolding holding, PairPressureStreet street) => street switch
	{
		PairPressureStreet.Third => holding.Door,
		PairPressureStreet.Fourth => holding.Fourth,
		PairPressureStreet.Fifth => holding.Fifth,
		PairPressureStreet.Sixth => holding.Sixth,
		_ => null
	};

	private static IReadOnlyList<int> LeftOfDealer(int dealerSeat) =>
		Enumerable.Range(1, PairPressureFiveMaxCatalog.SeatCount)
			.Select(offset => (dealerSeat + offset) % PairPressureFiveMaxCatalog.SeatCount)
			.ToArray();

	private static int ExpectedCards(PairPressureStreet street) => street switch
	{
		PairPressureStreet.Third => 3,
		PairPressureStreet.Fourth => 4,
		PairPressureStreet.Fifth => 5,
		PairPressureStreet.Sixth => 6,
		PairPressureStreet.Seventh => 7,
		_ => throw new ArgumentOutOfRangeException(nameof(street), street, "Unknown street.")
	};

	private static int CompareBringIn(string a, string b)
	{
		var values = RankValue(a).CompareTo(RankValue(b));
		return values != 0 ? values : SuitRank(a).CompareTo(SuitRank(b));
	}

	/// <summary>
	///     Natural board strength used to choose the first actor from fourth street on.
	///     Matches the seven-card stud visible-hand ordering: pairs and high cards, then the
	///     suit of the highest card. Clubs, diamonds, hearts, spades.
	/// </summary>
	private static long VisibleStrength(PairPressureHolding holding, PairPressureStreet street)
	{
		var cards = VisibleCards(holding, street)
			.Select(card => (Value: RankValue(card), Suit: SuitRank(card)))
			.OrderByDescending(card => card.Value)
			.ToList();
		if (cards.Count == 0)
			return 0;

		var valueCounts = cards
			.GroupBy(card => card.Value)
			.OrderByDescending(group => group.Count())
			.ThenByDescending(group => group.Key)
			.ToList();
		var maxCount = valueCounts[0].Count();
		long strength;
		if (maxCount >= 4)
			strength = 7_000_000 + valueCounts[0].Key * 1000;
		else if (maxCount >= 3)
			strength = 4_000_000 + valueCounts[0].Key * 1000;
		else if (maxCount >= 2)
		{
			var pairs = valueCounts.Where(group => group.Count() >= 2).ToList();
			strength = pairs.Count >= 2
				? 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10
				: 2_000_000 + pairs[0].Key * 1000;
		}
		else
		{
			strength = 1_000_000;
		}

		foreach (var card in cards.Take(4))
			strength = strength * 15 + card.Value;

		return strength * 4 + cards[0].Suit;
	}

	private static IReadOnlyList<string> VisibleCards(PairPressureHolding holding, PairPressureStreet street)
	{
		var cards = new List<string> { holding.Door };
		if (street >= PairPressureStreet.Fourth && holding.Fourth is not null)
			cards.Add(holding.Fourth);
		if (street >= PairPressureStreet.Fifth && holding.Fifth is not null)
			cards.Add(holding.Fifth);
		if (street >= PairPressureStreet.Sixth && holding.Sixth is not null)
			cards.Add(holding.Sixth);
		return cards;
	}

	private static int RankValue(string card)
	{
		return Normalize(card)[0] switch
		{
			'A' => 14,
			'K' => 13,
			'Q' => 12,
			'J' => 11,
			'T' => 10,
			_ => Normalize(card)[0] - '0'
		};
	}

	private static int SuitRank(string card)
	{
		return Normalize(card)[1] switch
		{
			'c' => 0,
			'd' => 1,
			'h' => 2,
			's' => 3,
			_ => 0
		};
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

	private sealed class SeatPlan
	{
		private readonly PairPressureHandScript _script;

		private SeatPlan(PairPressureHandScript script)
		{
			_script = script;
		}

		public required HoldEmPosition Position { get; init; }

		public required Queue<string> Queue { get; init; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		public static SeatPlan Create(
			HoldEmPosition position,
			PairPressureHolding holding,
			PairPressureHandScript script)
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

		public bool StillIn(PairPressureStreet street) =>
			!_script.Actions.Any(action =>
				action.Actor == Position &&
				action.Kind == PairPressureActionKind.Fold &&
				action.Street < street);

		public PairPressureDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record PairPressureDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, PairPressureDealtCards> Seats { get; init; }
}

public sealed record PairPressureDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
