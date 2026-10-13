namespace CardGames.E2E.Infrastructure;

public enum KingsAndLowsActionKind
{
	Drop,
	Stay,
	StandPat,
	Discard,
	DeckKeepAll
}

public enum KingsAndLowsShowdownKind
{
	Winner,
	Carried,
	DeckWon
}

/// <summary>
///     One drop, stay, draw, or deck decision. Discards and draws are card faces.
/// </summary>
public sealed record KingsAndLowsAction(
	HoldEmPosition Actor,
	KingsAndLowsActionKind Kind,
	IReadOnlyList<string>? Discards = null,
	IReadOnlyList<string>? Draws = null)
{
	public static KingsAndLowsAction Drop(HoldEmPosition actor) =>
		new(actor, KingsAndLowsActionKind.Drop);

	public static KingsAndLowsAction Stay(HoldEmPosition actor) =>
		new(actor, KingsAndLowsActionKind.Stay);

	public static KingsAndLowsAction StandPat(HoldEmPosition actor) =>
		new(actor, KingsAndLowsActionKind.StandPat);

	public static KingsAndLowsAction Discard(
		HoldEmPosition actor,
		IReadOnlyList<string> discards,
		IReadOnlyList<string> draws) =>
		new(actor, KingsAndLowsActionKind.Discard, discards, draws);

	public static KingsAndLowsAction DeckKeepAll(HoldEmPosition actor) =>
		new(actor, KingsAndLowsActionKind.DeckKeepAll);
}

/// <summary>
///     A five-handed Kings and Lows deal. Nets are the stack change when the hand is over.
///     Chips that leave the stacks and are not paid to a winner sit in <see cref="NextPot" />.
/// </summary>
public sealed record KingsAndLowsHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<KingsAndLowsAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required int OpeningPot { get; init; }

	public required int NextPot { get; init; }

	public required KingsAndLowsShowdownKind Showdown { get; init; }

	public HoldEmPosition? Winner { get; init; }

	public int AwardedAmount { get; init; }

	/// <summary>
	///     The five cards dealt to the deck after the stayer draws. Set only for player versus deck.
	/// </summary>
	public IReadOnlyList<string>? DeckCards { get; init; }
}

/// <summary>
///     Running table stacks plus the pot carried into the next hand.
/// </summary>
public sealed class KingsAndLowsLedger
{
	private readonly int[] _stacks;

	public KingsAndLowsLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public int CarriedPot { get; private set; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(KingsAndLowsHandScript script, int dealerSeat) =>
		Apply(script.Name, script.Nets, dealerSeat, script.NextPot);

	public void Apply(
		string handName,
		IReadOnlyDictionary<HoldEmPosition, int> nets,
		int dealerSeat,
		int nextPot)
	{
		foreach (var (position, net) in nets)
			_stacks[HoldEmSeats.Index(_stacks.Length, dealerSeat, position)] += net;

		CarriedPot = nextPot;
		var conserved = _stacks.Sum() + CarriedPot;
		if (conserved != StartingSum)
		{
			throw new InvalidOperationException(
				$"Hand '{handName}' changed the table chip total from {StartingSum} to {conserved}.");
		}
	}
}

/// <summary>
///     Known deals for the five-player Kings and Lows Playwright orbit. The ante is 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     Cards are dealt by ascending seat. Draws start with the first stayer left of the dealer.
/// </summary>
public static class KingsAndLowsFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 10;
	public const int FirstPot = Ante * SeatCount;

	/// <summary>
	///     Clockwise order, starting left of the dealer.
	/// </summary>
	public static readonly HoldEmPosition[] ActionOrder =
	[
		HoldEmPosition.SmallBlind,
		HoldEmPosition.BigBlind,
		HoldEmPosition.UnderTheGun,
		HoldEmPosition.Cutoff,
		HoldEmPosition.Button
	];

	public static KingsAndLowsHandScript FiveOfAKind { get; } = new()
	{
		Name = "Five of a kind after mixed draws",
		HoleCards = Holes(
			"Jh Jd 9h 5d 5h",
			"Ah Ad As Ac Kh",
			"Qh Qd Js Tc 2h",
			"2c 2d 3h 3s 5c",
			"4h 4d 3c 3d 2s"),
		Actions =
		[
			KingsAndLowsAction.Stay(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.Stay(HoldEmPosition.BigBlind),
			KingsAndLowsAction.Stay(HoldEmPosition.UnderTheGun),
			KingsAndLowsAction.Stay(HoldEmPosition.Cutoff),
			KingsAndLowsAction.Stay(HoldEmPosition.Button),
			KingsAndLowsAction.StandPat(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.Discard(HoldEmPosition.BigBlind, ["2h"], ["9c"]),
			KingsAndLowsAction.Discard(
				HoldEmPosition.UnderTheGun,
				["2c", "2d", "3h", "3s", "5c"],
				["8h", "8d", "7c", "6d", "5s"]),
			KingsAndLowsAction.StandPat(HoldEmPosition.Cutoff),
			KingsAndLowsAction.Discard(HoldEmPosition.Button, ["5d", "5h"], ["7d", "6c"])
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = FirstPot - Ante,
			[HoldEmPosition.BigBlind] = -(Ante + FirstPot),
			[HoldEmPosition.UnderTheGun] = -(Ante + FirstPot),
			[HoldEmPosition.Cutoff] = -(Ante + FirstPot),
			[HoldEmPosition.Button] = -(Ante + FirstPot)
		},
		OpeningPot = FirstPot,
		NextPot = FirstPot * 4,
		Showdown = KingsAndLowsShowdownKind.Winner,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = FirstPot
	};

	public static KingsAndLowsHandScript AceLowStraight { get; } = new()
	{
		Name = "Ace-low straight beats an eight-high straight",
		HoleCards = Holes(
			"2c 2d 3h 3s 4h",
			"5h 6d 7c 8s Ac",
			"3c 4d 5s 6h 7d",
			"Qh Qd 9d 7s 2s",
			"4c 4s 8h 8d 9h"),
		Actions =
		[
			KingsAndLowsAction.Stay(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.Stay(HoldEmPosition.BigBlind),
			KingsAndLowsAction.Stay(HoldEmPosition.UnderTheGun),
			KingsAndLowsAction.Drop(HoldEmPosition.Cutoff),
			KingsAndLowsAction.Drop(HoldEmPosition.Button),
			KingsAndLowsAction.StandPat(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.StandPat(HoldEmPosition.BigBlind),
			KingsAndLowsAction.Discard(HoldEmPosition.UnderTheGun, ["2s"], ["3d"])
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 200,
			[HoldEmPosition.BigBlind] = -200,
			[HoldEmPosition.UnderTheGun] = -200,
			[HoldEmPosition.Cutoff] = 0,
			[HoldEmPosition.Button] = 0
		},
		OpeningPot = 200,
		NextPot = 400,
		Showdown = KingsAndLowsShowdownKind.Winner,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 200
	};

	public static KingsAndLowsHandScript EveryoneDrops { get; } = new()
	{
		Name = "Everyone drops and the pot carries",
		HoleCards = Holes(
			"2c 3c 4c 5c 6c",
			"7c 8c 9c Tc Jc",
			"Qc Kc Ac 2d 3d",
			"4d 5d 6d 7d 8d",
			"9d Td Jd Qd Kd"),
		Actions =
		[
			KingsAndLowsAction.Drop(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.Drop(HoldEmPosition.BigBlind),
			KingsAndLowsAction.Drop(HoldEmPosition.UnderTheGun),
			KingsAndLowsAction.Drop(HoldEmPosition.Cutoff),
			KingsAndLowsAction.Drop(HoldEmPosition.Button)
		],
		Nets = ActionOrder.ToDictionary(position => position, _ => 0),
		OpeningPot = 400,
		NextPot = 400,
		Showdown = KingsAndLowsShowdownKind.Carried
	};

	public static KingsAndLowsHandScript DeckWins { get; } = new()
	{
		Name = "The deck wins and the pot carries",
		HoleCards = Holes(
			"Ah Ad As 2c 3c",
			"9h 8d 7c 6s 4h",
			"4c 5c 6c 7d 8c",
			"5d 6d 7h 8h 9c",
			"2d 3h 4d 5h 6h"),
		Actions =
		[
			KingsAndLowsAction.Stay(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.Drop(HoldEmPosition.BigBlind),
			KingsAndLowsAction.Drop(HoldEmPosition.UnderTheGun),
			KingsAndLowsAction.Drop(HoldEmPosition.Cutoff),
			KingsAndLowsAction.Drop(HoldEmPosition.Button),
			KingsAndLowsAction.StandPat(HoldEmPosition.SmallBlind),
			KingsAndLowsAction.DeckKeepAll(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = -400,
			[HoldEmPosition.BigBlind] = 0,
			[HoldEmPosition.UnderTheGun] = 0,
			[HoldEmPosition.Cutoff] = 0,
			[HoldEmPosition.Button] = 0
		},
		OpeningPot = 400,
		NextPot = 800,
		Showdown = KingsAndLowsShowdownKind.DeckWon,
		DeckCards = ["Kh", "Kd", "Kc", "2h", "3d"]
	};

	/// <summary>
	///     Four hands. The scenario deals hand <c>n</c> from seat <c>n</c>.
	/// </summary>
	public static IReadOnlyList<KingsAndLowsHandScript> Orbit { get; } =
	[
		FiveOfAKind,
		AceLowStraight,
		EveryoneDrops,
		DeckWins
	];

	public static IReadOnlyList<HoldEmPosition> Stayers(KingsAndLowsHandScript script)
	{
		var stayed = script.Actions
			.Where(action => action.Kind == KingsAndLowsActionKind.Stay)
			.Select(action => action.Actor)
			.ToHashSet();
		return ActionOrder.Where(stayed.Contains).ToArray();
	}

	/// <summary>
	///     Draw actors in server order. Empty when everybody dropped.
	///     The first actor is the first stayer left of the dealer, then clockwise.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DrawOrder(KingsAndLowsHandScript script) =>
		Stayers(script);

	/// <summary>
	///     The dealer decides the deck draw unless the dealer is the only stayer.
	/// </summary>
	public static HoldEmPosition DeckDecisionMaker(KingsAndLowsHandScript script)
	{
		var stayers = Stayers(script);
		if (stayers.Count != 1)
			throw new InvalidOperationException($"'{script.Name}' is not a player-versus-deck hand.");

		return stayers[0] == HoldEmPosition.Button
			? HoldEmPosition.SmallBlind
			: HoldEmPosition.Button;
	}

	/// <summary>
	///     Replacement cards, then the deck hand, in the order the stub is consumed.
	/// </summary>
	public static IReadOnlyList<string> DrawStub(KingsAndLowsHandScript script)
	{
		var drawsByActor = script.Actions
			.Where(action => action.Kind == KingsAndLowsActionKind.Discard)
			.ToDictionary(action => action.Actor, action => action.Draws ?? []);

		var draws = DrawOrder(script)
			.Where(drawsByActor.ContainsKey)
			.SelectMany(position => drawsByActor[position]);
		return script.DeckCards is null
			? draws.ToArray()
			: draws.Concat(script.DeckCards).ToArray();
	}

	public static IReadOnlyList<string> FinalHand(KingsAndLowsHandScript script, HoldEmPosition actor)
	{
		var discard = script.Actions.FirstOrDefault(action =>
			action.Actor == actor && action.Kind == KingsAndLowsActionKind.Discard);
		var thrown = new HashSet<string>(discard?.Discards ?? [], StringComparer.Ordinal);
		return script.HoleCards[actor]
			.Where(card => !thrown.Contains(card))
			.Concat(discard?.Draws ?? [])
			.ToArray();
	}

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Holes(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff)
	{
		return new Dictionary<HoldEmPosition, IReadOnlyList<string>>
		{
			[HoldEmPosition.Button] = Five(button),
			[HoldEmPosition.SmallBlind] = Five(smallBlind),
			[HoldEmPosition.BigBlind] = Five(bigBlind),
			[HoldEmPosition.UnderTheGun] = Five(underTheGun),
			[HoldEmPosition.Cutoff] = Five(cutoff)
		};
	}

	private static IReadOnlyList<string> Five(string cards) =>
		cards.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
