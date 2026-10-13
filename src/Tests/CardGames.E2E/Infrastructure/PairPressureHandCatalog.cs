namespace CardGames.E2E.Infrastructure;

public enum PairPressureStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum PairPressureActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

/// <summary>
///     One Pair Pressure action. Bet and raise amounts are the street total the action panel submits.
///     <see cref="WildLabels" /> are the ranks the wild-card panel should show before the action,
///     oldest active pair first, such as "Eights" then "Fives". An empty list means the panel is empty.
/// </summary>
public sealed record PairPressureAction(
	HoldEmPosition Actor,
	PairPressureActionKind Kind,
	PairPressureStreet Street,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null,
	IReadOnlyList<string>? WildLabels = null)
{
	public static PairPressureAction Fold(
		HoldEmPosition actor,
		PairPressureStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(actor, PairPressureActionKind.Fold, street, UpCards: upCards, WildLabels: wildLabels);

	public static PairPressureAction Check(
		HoldEmPosition actor,
		PairPressureStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(actor, PairPressureActionKind.Check, street, UpCards: upCards, WildLabels: wildLabels);

	public static PairPressureAction Call(
		HoldEmPosition actor,
		PairPressureStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(
			actor,
			PairPressureActionKind.Call,
			street,
			PotAfter: potAfter,
			CallAmount: callAmount,
			StreetBet: streetBet,
			WildLabels: wildLabels);

	public static PairPressureAction Bet(
		HoldEmPosition actor,
		PairPressureStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(
			actor,
			PairPressureActionKind.Bet,
			street,
			amount,
			PotAfter: potAfter,
			StreetBet: amount,
			UpCards: upCards,
			WildLabels: wildLabels);

	public static PairPressureAction Raise(
		HoldEmPosition actor,
		PairPressureStreet street,
		int raiseTo,
		int potAfter) =>
		new(actor, PairPressureActionKind.RaiseTo, street, raiseTo, PotAfter: potAfter, StreetBet: raiseTo);
}

/// <summary>
///     Cards a seat receives, in deal order.
/// </summary>
public sealed record PairPressureHolding
{
	public required string FirstHole { get; init; }

	public required string SecondHole { get; init; }

	public required string Door { get; init; }

	public string? Fourth { get; init; }

	public string? Fifth { get; init; }

	public string? Sixth { get; init; }

	public string? Seventh { get; init; }
}

/// <summary>
///     A five-handed Pair Pressure deal and the line played through the UI.
///     Nets are the stack change when the hand is over. Positions are offsets from the dealer.
/// </summary>
public sealed record PairPressureHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, PairPressureHolding> Holdings { get; init; }

	public required IReadOnlyList<PairPressureAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }

	public bool ShowsWild { get; init; }
}

public sealed class PairPressureLedger
{
	private readonly int[] _stacks;

	public PairPressureLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(PairPressureHandScript script, int dealerSeat)
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
///     Known deals for the five-player Pair Pressure orbit.
///     Ante 5, bring-in 10, small bet 10, big bet 20.
///     A face-up rank becomes wild the second time it appears. Only the two most recent
///     distinct paired ranks stay wild. The bring-in is the big blind's deuce in every hand.
/// </summary>
public static class PairPressureFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = 35;

	public const string Eights = "Eights";
	public const string Fives = "Fives";
	public const string Kings = "Kings";

	public static readonly IReadOnlyList<string> NoWilds = [];

	public static PairPressureHandScript FoldToBringIn { get; } = new()
	{
		Name = "Fold to the bring-in",
		Holdings = Hands(
			Holes("7d 8d", "Th"),
			Holes("5h 6h", "9h"),
			Holes("5d 6d", "2c"),
			Holes("5c 6c", "Kh"),
			Holes("5s 6s", "Jh")),
		Actions =
		[
			PairPressureAction.Fold(HoldEmPosition.UnderTheGun, PairPressureStreet.Third, Doors(
				"Th", "9h", "2c", "Kh", "Jh"), NoWilds),
			PairPressureAction.Fold(HoldEmPosition.Cutoff, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.Button, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.SmallBlind, PairPressureStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The small blind and under the gun show eights, so eights are the only wild rank.
	///     Fourth street does not pair another rank.
	/// </summary>
	public static PairPressureHandScript FirstPair { get; } = new()
	{
		Name = "The first face-up pair becomes wild",
		Holdings = Hands(
			Holes("4c 3h", "Th"),
			Holes("5h 6h", "8h"),
			Holes("5d 6d", "2c", fourth: "Ah"),
			Holes("5c 6c", "8d", fourth: "4s"),
			Holes("5s 6s", "Kh")),
		Actions =
		[
			PairPressureAction.Call(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Eights]),
			PairPressureAction.Fold(HoldEmPosition.Cutoff, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.Button, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.SmallBlind, PairPressureStreet.Third),
			PairPressureAction.Bet(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Fourth,
				amount: SmallBet,
				potAfter: 55,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["2c", "Ah"],
					[HoldEmPosition.UnderTheGun] = ["8d", "4s"]
				},
				wildLabels: [Eights]),
			PairPressureAction.Fold(HoldEmPosition.UnderTheGun, PairPressureStreet.Fourth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 30,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 55,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     Eights pair, then fives. Kings on fourth street evict eights.
	///     The big blind's eight on fifth street does not bring eights back.
	///     Under the gun acts first on fourth and fifth: king-eight-four beats king-eight-deuce.
	/// </summary>
	public static PairPressureHandScript EvictOldestPair { get; } = new()
	{
		Name = "A third distinct pair evicts the oldest wild rank",
		Holdings = Hands(
			Holes("4c 3h", "5s"),
			Holes("5h 6h", "8h"),
			Holes("5d 6d", "2c", fourth: "Kh", fifth: "8s"),
			Holes("7c 8c", "8d", fourth: "Kd", fifth: "4s"),
			Holes("7d 9d", "5c")),
		Actions =
		[
			PairPressureAction.Call(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Eights, Fives]),
			PairPressureAction.Fold(HoldEmPosition.Cutoff, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.Button, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.SmallBlind, PairPressureStreet.Third),
			PairPressureAction.Check(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Fourth,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["2c", "Kh"],
					[HoldEmPosition.UnderTheGun] = ["8d", "Kd"]
				},
				wildLabels: [Fives, Kings]),
			PairPressureAction.Check(HoldEmPosition.BigBlind, PairPressureStreet.Fourth),
			PairPressureAction.Check(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Fifth,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["2c", "Kh", "8s"],
					[HoldEmPosition.UnderTheGun] = ["8d", "Kd", "4s"]
				},
				wildLabels: [Fives, Kings]),
			PairPressureAction.Bet(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Fifth,
				amount: BigBet,
				potAfter: 65),
			PairPressureAction.Fold(HoldEmPosition.UnderTheGun, PairPressureStreet.Fifth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 30,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 65,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     Kings pair on fifth street and evict eights. The big blind's two wild kings make four aces.
	///     Under the gun's wild five and wild king make four queens. The door eight would make five queens
	///     if eights were still wild.
	/// </summary>
	public static PairPressureHandScript WildShowdown { get; } = new()
	{
		Name = "Wild-card showdown",
		Holdings = Hands(
			Holes("6s 7s", "5s"),
			Holes("6c 9d", "8h"),
			Holes("Ah Ad", "2c", fourth: "Kh", fifth: "Kd", sixth: "4s", seventh: "3d"),
			Holes("Qc Qd", "8d", fourth: "5d", fifth: "7h", sixth: "9h", seventh: "Ks"),
			Holes("6d 7d", "5c")),
		Actions =
		[
			PairPressureAction.Call(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Eights, Fives]),
			PairPressureAction.Fold(HoldEmPosition.Cutoff, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.Button, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.SmallBlind, PairPressureStreet.Third),
			PairPressureAction.Check(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Fourth,
				wildLabels: [Eights, Fives]),
			PairPressureAction.Check(HoldEmPosition.UnderTheGun, PairPressureStreet.Fourth),
			PairPressureAction.Check(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Fifth,
				wildLabels: [Fives, Kings]),
			PairPressureAction.Check(HoldEmPosition.UnderTheGun, PairPressureStreet.Fifth),
			PairPressureAction.Check(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Sixth,
				wildLabels: [Fives, Kings]),
			PairPressureAction.Check(HoldEmPosition.UnderTheGun, PairPressureStreet.Sixth),
			PairPressureAction.Check(HoldEmPosition.BigBlind, PairPressureStreet.Seventh),
			PairPressureAction.Check(HoldEmPosition.UnderTheGun, PairPressureStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 30,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 45,
		OpeningPot = OpeningPot,
		WonByFold = false,
		ShowsWild = true
	};

	/// <summary>
	///     No face-up rank pairs. Fifth street is a big-bet raise, and the big blind folds.
	/// </summary>
	public static PairPressureHandScript BigBetRaise { get; } = new()
	{
		Name = "Big-bet raise",
		Holdings = Hands(
			Holes("4c 3h", "Th"),
			Holes("5h 6h", "9h"),
			Holes("5d 6d", "2c", fourth: "Ah", fifth: "3d"),
			Holes("5c 6c", "Kh", fourth: "4s", fifth: "7c"),
			Holes("5s 6s", "Jh")),
		Actions =
		[
			PairPressureAction.Call(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: NoWilds),
			PairPressureAction.Fold(HoldEmPosition.Cutoff, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.Button, PairPressureStreet.Third),
			PairPressureAction.Fold(HoldEmPosition.SmallBlind, PairPressureStreet.Third),
			PairPressureAction.Check(HoldEmPosition.BigBlind, PairPressureStreet.Fourth, wildLabels: NoWilds),
			PairPressureAction.Check(HoldEmPosition.UnderTheGun, PairPressureStreet.Fourth),
			PairPressureAction.Bet(
				HoldEmPosition.BigBlind,
				PairPressureStreet.Fifth,
				amount: BigBet,
				potAfter: 65,
				wildLabels: NoWilds),
			PairPressureAction.Raise(
				HoldEmPosition.UnderTheGun,
				PairPressureStreet.Fifth,
				raiseTo: BigBet * 2,
				potAfter: 105),
			PairPressureAction.Fold(HoldEmPosition.BigBlind, PairPressureStreet.Fifth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 50,
			[HoldEmPosition.BigBlind] = -35,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 105,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static IReadOnlyList<PairPressureHandScript> Orbit { get; } =
	[
		FoldToBringIn,
		FirstPair,
		EvictOldestPair,
		WildShowdown,
		BigBetRaise
	];

	private static Dictionary<HoldEmPosition, int> LoseAntes(HoldEmPosition winner, int winnerNet)
	{
		var nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		};
		nets[winner] = winnerNet;
		return nets;
	}

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Doors(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = [button],
			[HoldEmPosition.SmallBlind] = [smallBlind],
			[HoldEmPosition.BigBlind] = [bigBlind],
			[HoldEmPosition.UnderTheGun] = [underTheGun],
			[HoldEmPosition.Cutoff] = [cutoff]
		};

	private static Dictionary<HoldEmPosition, PairPressureHolding> Hands(
		PairPressureHolding button,
		PairPressureHolding smallBlind,
		PairPressureHolding bigBlind,
		PairPressureHolding underTheGun,
		PairPressureHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static PairPressureHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null)
	{
		var cards = hole.Split(' ');
		return new PairPressureHolding
		{
			FirstHole = cards[0],
			SecondHole = cards[1],
			Door = door,
			Fourth = fourth,
			Fifth = fifth,
			Sixth = sixth,
			Seventh = seventh
		};
	}
}
