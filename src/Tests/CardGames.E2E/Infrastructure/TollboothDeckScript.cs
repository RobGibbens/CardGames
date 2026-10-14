namespace CardGames.E2E.Infrastructure;

public enum TollboothStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum TollboothStepKind
{
	Bet,
	Choose
}

public enum TollboothActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

public enum TollboothSource
{
	Furthest,
	Nearest,
	Deck
}

/// <summary>
///     One betting action or one Tollbooth choice.
///     Bet and raise amounts are the street total the action panel submits.
///     A choice records the display pair the player should see before clicking.
/// </summary>
public sealed record TollboothStep(
	TollboothStepKind Kind,
	HoldEmPosition Actor,
	TollboothStreet Street,
	TollboothActionKind? Action = null,
	TollboothSource? Source = null,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	string? Received = null,
	string? Replenishment = null,
	string? DisplayNearest = null,
	string? DisplayFurthest = null,
	bool FaceDown = false,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null)
{
	public static TollboothStep Fold(
		HoldEmPosition actor,
		TollboothStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(TollboothStepKind.Bet, actor, street, TollboothActionKind.Fold, UpCards: upCards);

	public static TollboothStep Check(
		HoldEmPosition actor,
		TollboothStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(TollboothStepKind.Bet, actor, street, TollboothActionKind.Check, UpCards: upCards);

	public static TollboothStep Call(
		HoldEmPosition actor,
		TollboothStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(
			TollboothStepKind.Bet,
			actor,
			street,
			TollboothActionKind.Call,
			PotAfter: potAfter,
			CallAmount: callAmount,
			StreetBet: streetBet,
			UpCards: upCards);

	public static TollboothStep Bet(
		HoldEmPosition actor,
		TollboothStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(
			TollboothStepKind.Bet,
			actor,
			street,
			TollboothActionKind.Bet,
			Amount: amount,
			PotAfter: potAfter,
			StreetBet: amount,
			UpCards: upCards);

	public static TollboothStep Raise(
		HoldEmPosition actor,
		TollboothStreet street,
		int raiseTo,
		int potAfter) =>
		new(
			TollboothStepKind.Bet,
			actor,
			street,
			TollboothActionKind.RaiseTo,
			Amount: raiseTo,
			PotAfter: potAfter,
			StreetBet: raiseTo);

	public static TollboothStep Choose(
		HoldEmPosition actor,
		TollboothStreet street,
		TollboothSource source,
		string received,
		string? replenishment,
		int potAfter,
		string nearest,
		string furthest) =>
		new(
			TollboothStepKind.Choose,
			actor,
			street,
			Source: source,
			PotAfter: potAfter,
			Received: received,
			Replenishment: replenishment,
			DisplayNearest: nearest,
			DisplayFurthest: furthest,
			FaceDown: street == TollboothStreet.Seventh);
}

/// <summary>
///     Cards a seat receives, in the order the server assigns them.
/// </summary>
public sealed record TollboothHolding
{
	public required string FirstHole { get; init; }

	public required string SecondHole { get; init; }

	public required string Door { get; init; }

	public string? Fourth { get; init; }

	public string? Fifth { get; init; }

	public string? Sixth { get; init; }

	public string? Seventh { get; init; }

	public IReadOnlyList<string> Cards
	{
		get
		{
			var cards = new List<string> { FirstHole, SecondHole, Door };
			if (Fourth is not null) cards.Add(Fourth);
			if (Fifth is not null) cards.Add(Fifth);
			if (Sixth is not null) cards.Add(Sixth);
			if (Seventh is not null) cards.Add(Seventh);
			return cards;
		}
	}

	public IReadOnlyList<string> UpCards
	{
		get
		{
			var cards = new List<string> { Door };
			if (Fourth is not null) cards.Add(Fourth);
			if (Fifth is not null) cards.Add(Fifth);
			if (Sixth is not null) cards.Add(Sixth);
			return cards;
		}
	}
}

/// <summary>
///     A five-handed Tollbooth deal and the line played through the UI.
///     Nets are the stack change when the hand is over. Positions are offsets from the dealer.
/// </summary>
public sealed record TollboothHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, TollboothHolding> Holdings { get; init; }

	public required IReadOnlyList<TollboothStep> Steps { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public required string InitialNearest { get; init; }

	public required string InitialFurthest { get; init; }

	public required string ShowdownNearest { get; init; }

	public required string ShowdownFurthest { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }
}

public sealed class TollboothLedger
{
	private readonly int[] _stacks;

	public TollboothLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(TollboothHandScript script, int dealerSeat)
	{
		foreach (var (position, net) in script.Nets)
			_stacks[HoldEmSeats.Index(_stacks.Length, dealerSeat, position)] += net;

		var sum = _stacks.Sum();
		if (sum != StartingSum)
		{
			throw new InvalidOperationException(
				$"Hand '{script.Name}' changed the table chip total from {StartingSum} to {sum}.");
		}
	}
}

/// <summary>
///     Builds a 52-card Tollbooth deck in the order the server deals it.
///     Third street is two hole cards and one door, clockwise from the seat left of the dealer.
///     The next two cards are the display: first is nearest, second is furthest.
///     Later cards leave the deck only when a player takes the deck or a display card is replenished.
/// </summary>
public static class TollboothDeckScript
{
	private static readonly string[] StandardDeck =
	[
		"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
		"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
		"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
		"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
	];

	public static int Cost(TollboothSource source) => source switch
	{
		TollboothSource.Furthest => 0,
		TollboothSource.Nearest => TollboothFiveMaxCatalog.Ante,
		TollboothSource.Deck => TollboothFiveMaxCatalog.Ante * 2,
		_ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown Tollbooth choice.")
	};

	public static IReadOnlyList<string> Build(int dealerSeat, TollboothHandScript script) =>
		Trace(dealerSeat, script).Deck;

	/// <summary>
	///     The first chooser is the first live seat left of the dealer.
	///     Everyone after that is the lowest seat that has not folded and has not chosen yet.
	/// </summary>
	public static IReadOnlyList<int> ChoiceSeats(int dealerSeat, IReadOnlySet<int> folded)
	{
		var first = -1;
		for (var offset = 1; offset <= TollboothFiveMaxCatalog.SeatCount; offset++)
		{
			var seat = (dealerSeat + offset) % TollboothFiveMaxCatalog.SeatCount;
			if (folded.Contains(seat))
				continue;

			first = seat;
			break;
		}

		if (first < 0)
			throw new ArgumentException("Every seat has folded.");

		var order = new List<int> { first };
		for (var seat = 0; seat < TollboothFiveMaxCatalog.SeatCount; seat++)
		{
			if (seat != first && !folded.Contains(seat))
				order.Add(seat);
		}

		return order;
	}

	public static IReadOnlyList<HoldEmPosition> ChoiceOrder(
		int dealerSeat,
		IReadOnlySet<HoldEmPosition> folded)
	{
		var foldedSeats = folded
			.Select(position => HoldEmSeats.Index(TollboothFiveMaxCatalog.SeatCount, dealerSeat, position))
			.ToHashSet();
		return ChoiceSeats(dealerSeat, foldedSeats)
			.Select(seat => PositionOf(dealerSeat, seat))
			.ToArray();
	}

	/// <summary>
	///     The stud bring-in rank used by the server: clubs, diamonds, hearts, spades, then rank.
	/// </summary>
	public static long VisibleStrength(IReadOnlyList<string> boardCards)
	{
		if (boardCards.Count == 0)
			return 0;

		var cards = boardCards
			.Select(Normalize)
			.OrderByDescending(RankValue)
			.ToList();
		var valueCounts = cards
			.GroupBy(RankValue)
			.OrderByDescending(group => group.Count())
			.ThenByDescending(group => group.Key)
			.ToList();

		long strength = 0;
		var maxCount = valueCounts[0].Count();
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
			strength = strength * 15 + RankValue(card);

		if (cards.Count > 0)
			strength = strength * 4 + SuitRank(cards[0]);

		return strength;
	}

	public static TollboothDealTrace Trace(int dealerSeat, TollboothHandScript script)
	{
		if (script.Holdings.Count != TollboothFiveMaxCatalog.SeatCount)
			throw new ArgumentException("Tollbooth needs one holding for every seat.", nameof(script));

		var plans = new SeatPlan[TollboothFiveMaxCatalog.SeatCount];
		foreach (var (position, holding) in script.Holdings)
		{
			var seat = HoldEmSeats.Index(TollboothFiveMaxCatalog.SeatCount, dealerSeat, position);
			if (plans[seat] is not null)
				throw new ArgumentException($"Seat {seat} is assigned twice.");

			plans[seat] = SeatPlan.Create(position, holding);
		}

		var deck = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		for (var offset = 1; offset <= TollboothFiveMaxCatalog.SeatCount; offset++)
		{
			var plan = plans[(dealerSeat + offset) % TollboothFiveMaxCatalog.SeatCount]!;
			for (var cardIndex = 0; cardIndex < 3; cardIndex++)
			{
				var card = TakeNext(plan, deck, used);
				if (cardIndex == 2)
					plan.Up.Add(card);
			}
		}

		var display = new List<string>
		{
			TakeNew(script.InitialNearest, deck, used),
			TakeNew(script.InitialFurthest, deck, used)
		};
		var choices = new List<TollboothChoiceTrace>();
		foreach (var step in script.Steps.Where(step => step.Kind == TollboothStepKind.Choose))
			choices.Add(ApplyChoice(script, step, plans, dealerSeat, display, deck, used));

		if (display.Count != 2 ||
		    !string.Equals(display[0], Normalize(script.ShowdownNearest), StringComparison.Ordinal) ||
		    !string.Equals(display[^1], Normalize(script.ShowdownFurthest), StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"Hand '{script.Name}' ends with display {string.Join(" ", display)} " +
				$"instead of {script.ShowdownNearest} {script.ShowdownFurthest}.");
		}

		foreach (var plan in plans)
		{
			var expected = plan!.Holding.Cards.Select(Normalize).ToArray();
			if (!plan.Received.SequenceEqual(expected, StringComparer.Ordinal))
			{
				throw new ArgumentException(
					$"Hand '{script.Name}' dealt {plan.Position} [{string.Join(" ", plan.Received)}] " +
					$"instead of [{string.Join(" ", expected)}].");
			}
		}

		foreach (var card in StandardDeck)
		{
			if (used.Add(card))
				deck.Add(card);
		}

		if (deck.Count != StandardDeck.Length || deck.Distinct(StringComparer.Ordinal).Count() != StandardDeck.Length)
			throw new ArgumentException($"Hand '{script.Name}' did not contain a valid 52-card deck.");

		return new TollboothDealTrace
		{
			Deck = deck,
			Choices = choices,
			FinalNearest = display[0],
			FinalFurthest = display[^1],
			Seats = plans.ToDictionary(plan => plan!.Position, plan => plan!.Snapshot())
		};
	}

	private static TollboothChoiceTrace ApplyChoice(
		TollboothHandScript script,
		TollboothStep step,
		SeatPlan[] plans,
		int dealerSeat,
		List<string> display,
		List<string> deck,
		HashSet<string> used)
	{
		if (step.Source is null || step.Received is null || step.DisplayNearest is null || step.DisplayFurthest is null)
			throw new ArgumentException($"Hand '{script.Name}' has a choice without a card.");

		if (display.Count != 2)
			throw new ArgumentException($"Hand '{script.Name}' does not have two display cards.");

		var nearest = display[0];
		var furthest = display[^1];
		if (!string.Equals(nearest, Normalize(step.DisplayNearest), StringComparison.Ordinal) ||
		    !string.Equals(furthest, Normalize(step.DisplayFurthest), StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"Hand '{script.Name}' {step.Actor} on {step.Street} expected display " +
				$"{step.DisplayNearest} {step.DisplayFurthest} but found {nearest} {furthest}.");
		}

		var received = step.Source switch
		{
			TollboothSource.Furthest => TakeDisplay(display, furthestIndex: true, step, deck, used),
			TollboothSource.Nearest => TakeDisplay(display, furthestIndex: false, step, deck, used),
			TollboothSource.Deck => TakeNew(step.Received, deck, used),
			_ => throw new ArgumentOutOfRangeException(nameof(step), step.Source, "Unknown Tollbooth choice.")
		};

		if (!string.Equals(received, Normalize(step.Received), StringComparison.Ordinal))
		{
			throw new ArgumentException(
				$"Hand '{script.Name}' {step.Actor} on {step.Street} received {received} instead of {step.Received}.");
		}

		var seat = HoldEmSeats.Index(TollboothFiveMaxCatalog.SeatCount, dealerSeat, step.Actor);
		var plan = plans[seat]!;
		plan.Received.Add(received);
		if (!step.FaceDown)
			plan.Up.Add(received);

		return new TollboothChoiceTrace
		{
			Actor = step.Actor,
			Street = step.Street,
			Source = step.Source.Value,
			Received = received,
			NearestBefore = nearest,
			FurthestBefore = furthest,
			NearestAfter = display[0],
			FurthestAfter = display[^1],
			FaceDown = step.FaceDown,
			Cost = Cost(step.Source.Value)
		};
	}

	private static string TakeDisplay(
		List<string> display,
		bool furthestIndex,
		TollboothStep step,
		List<string> deck,
		HashSet<string> used)
	{
		if (step.Replenishment is null)
			throw new ArgumentException($"{step.Actor} must replenish a display card.");

		var index = furthestIndex ? display.Count - 1 : 0;
		var received = display[index];
		display.RemoveAt(index);
		display.Add(TakeNew(step.Replenishment, deck, used));
		return received;
	}

	private static string TakeNext(SeatPlan plan, List<string> deck, HashSet<string> used)
	{
		if (plan.Queue.Count == 0)
			throw new InvalidOperationException($"Seat {plan.Position} ran out of scripted cards.");

		var card = TakeNew(plan.Queue.Dequeue(), deck, used);
		plan.Received.Add(card);
		return card;
	}

	private static string TakeNew(string card, List<string> deck, HashSet<string> used)
	{
		var normalized = Normalize(card);
		if (!used.Add(normalized))
			throw new ArgumentException($"Card '{card}' is repeated.");

		deck.Add(normalized);
		return normalized;
	}

	private static HoldEmPosition PositionOf(int dealerSeat, int seat)
	{
		var offset = (seat - dealerSeat + TollboothFiveMaxCatalog.SeatCount) % TollboothFiveMaxCatalog.SeatCount;
		return offset switch
		{
			0 => HoldEmPosition.Button,
			1 => HoldEmPosition.SmallBlind,
			2 => HoldEmPosition.BigBlind,
			3 => HoldEmPosition.UnderTheGun,
			4 => HoldEmPosition.Cutoff,
			_ => throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat is outside the five-handed table.")
		};
	}

	private static int RankValue(string card) => char.ToUpperInvariant(Normalize(card)[0]) switch
	{
		'A' => 14,
		'K' => 13,
		'Q' => 12,
		'J' => 11,
		'T' => 10,
		'2' => 2,
		'3' => 3,
		'4' => 4,
		'5' => 5,
		'6' => 6,
		'7' => 7,
		'8' => 8,
		'9' => 9,
		_ => throw new ArgumentException($"'{card}' is not a valid card.")
	};

	private static int SuitRank(string card) => char.ToLowerInvariant(Normalize(card)[1]) switch
	{
		'c' => 0,
		'd' => 1,
		'h' => 2,
		's' => 3,
		_ => throw new ArgumentException($"'{card}' is not a valid card.")
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

		public required TollboothHolding Holding { get; init; }

		public required Queue<string> Queue { get; init; }

		public List<string> Received { get; } = [];

		public List<string> Up { get; } = [];

		public static SeatPlan Create(HoldEmPosition position, TollboothHolding holding) =>
			new()
			{
				Position = position,
				Holding = holding,
				Queue = new Queue<string>([holding.FirstHole, holding.SecondHole, holding.Door])
			};

		public TollboothDealtCards Snapshot() => new()
		{
			Cards = Received.ToArray(),
			UpCards = Up.ToArray()
		};
	}
}

public sealed record TollboothDealTrace
{
	public required IReadOnlyList<string> Deck { get; init; }

	public required IReadOnlyList<TollboothChoiceTrace> Choices { get; init; }

	public required string FinalNearest { get; init; }

	public required string FinalFurthest { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, TollboothDealtCards> Seats { get; init; }
}

public sealed record TollboothChoiceTrace
{
	public required HoldEmPosition Actor { get; init; }

	public required TollboothStreet Street { get; init; }

	public required TollboothSource Source { get; init; }

	public required string Received { get; init; }

	public required string NearestBefore { get; init; }

	public required string FurthestBefore { get; init; }

	public required string NearestAfter { get; init; }

	public required string FurthestAfter { get; init; }

	public required bool FaceDown { get; init; }

	public required int Cost { get; init; }
}

public sealed record TollboothDealtCards
{
	public required IReadOnlyList<string> Cards { get; init; }

	public required IReadOnlyList<string> UpCards { get; init; }
}
