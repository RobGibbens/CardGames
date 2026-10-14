namespace CardGames.E2E.Infrastructure;

public enum SevenCardStudStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum SevenCardStudActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

/// <summary>
///     One Seven Card Stud action. Bet and raise amounts are the street total the action panel submits.
/// </summary>
public sealed record SevenCardStudAction(
	HoldEmPosition Actor,
	SevenCardStudActionKind Kind,
	SevenCardStudStreet Street,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null)
{
	public static SevenCardStudAction Fold(
		HoldEmPosition actor,
		SevenCardStudStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(actor, SevenCardStudActionKind.Fold, street, UpCards: upCards);

	public static SevenCardStudAction Check(
		HoldEmPosition actor,
		SevenCardStudStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(actor, SevenCardStudActionKind.Check, street, UpCards: upCards);

	public static SevenCardStudAction Call(
		HoldEmPosition actor,
		SevenCardStudStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(
			actor,
			SevenCardStudActionKind.Call,
			street,
			PotAfter: potAfter,
			CallAmount: callAmount,
			StreetBet: streetBet);

	public static SevenCardStudAction Bet(
		HoldEmPosition actor,
		SevenCardStudStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(
			actor,
			SevenCardStudActionKind.Bet,
			street,
			amount,
			PotAfter: potAfter,
			StreetBet: amount,
			UpCards: upCards);

	public static SevenCardStudAction Raise(
		HoldEmPosition actor,
		SevenCardStudStreet street,
		int raiseTo,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(
			actor,
			SevenCardStudActionKind.RaiseTo,
			street,
			raiseTo,
			PotAfter: potAfter,
			StreetBet: raiseTo,
			UpCards: upCards);
}

/// <summary>
///     Cards a seat receives, in deal order. Seventh street is face down.
/// </summary>
public sealed record SevenCardStudHolding
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
///     A five-handed Seven Card Stud deal and the line played through the UI.
///     Nets are the stack change when the hand is over. Positions are offsets from the dealer.
/// </summary>
public sealed record SevenCardStudHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, SevenCardStudHolding> Holdings { get; init; }

	public required IReadOnlyList<SevenCardStudAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }
}

public sealed class SevenCardStudLedger
{
	private readonly int[] _stacks;

	public SevenCardStudLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(SevenCardStudHandScript script, int dealerSeat)
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
///     Known deals for the five-player Seven Card Stud orbit.
///     Ante 5, bring-in 5, small bet 10, big bet 20.
///     The bring-in is the lowest door. Clubs are the lowest suit, then diamonds, hearts, and spades.
///     Third and fourth street bet 10. Fifth, sixth, and seventh street bet 20.
///     A pair on fourth street does not raise that limit.
/// </summary>
public static class SevenCardStudFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 5;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = 30;

	/// <summary>
	///     The big blind's deuce of clubs brings in ahead of the small blind's deuce of diamonds.
	///     Everyone else folds, so the bring-in wins the antes without showing a hand.
	/// </summary>
	public static SevenCardStudHandScript SuitDecidesTheBringIn { get; } = new()
	{
		Name = "Suit decides the bring-in",
		Holdings = Hands(
			Holes("5c 6c", "Kh"),
			Holes("5s 6s", "2d"),
			Holes("5h 6h", "2c"),
			Holes("5d 6d", "Jh"),
			Holes("7c 8c", "Th")),
		Actions =
		[
			SevenCardStudAction.Fold(HoldEmPosition.UnderTheGun, SevenCardStudStreet.Third, Doors(
				"Kh", "2d", "2c", "Jh", "Th")),
			SevenCardStudAction.Fold(HoldEmPosition.Cutoff, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.Button, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.SmallBlind, SevenCardStudStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The button completes the 5 bring-in by raising to the 10 small bet. The field folds.
	/// </summary>
	public static SevenCardStudHandScript CompleteTheBringIn { get; } = new()
	{
		Name = "Complete the bring-in",
		Holdings = Hands(
			Holes("5c 6c", "Qh"),
			Holes("5s 6s", "9h"),
			Holes("7s 8s", "8h"),
			Holes("7d 9d", "7h"),
			Holes("4s 6d", "3c")),
		Actions =
		[
			SevenCardStudAction.Raise(
				HoldEmPosition.Button,
				SevenCardStudStreet.Third,
				raiseTo: SmallBet,
				potAfter: 40,
				upCards: Doors("Qh", "9h", "8h", "7h", "3c")),
			SevenCardStudAction.Fold(HoldEmPosition.SmallBlind, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.BigBlind, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.UnderTheGun, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.Cutoff, SevenCardStudStreet.Third)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 25,
			[HoldEmPosition.Cutoff] = -10,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante
		},
		BringIn = HoldEmPosition.Cutoff,
		Winner = HoldEmPosition.Button,
		AwardedAmount = 40,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The cutoff pairs kings on fourth street and bets 10. The small blind's ace-high is the
	///     seat left of the dealer, so the pair is first to act only because it is the best board.
	/// </summary>
	public static SevenCardStudHandScript PairActsForTheSmallBet { get; } = new()
	{
		Name = "A pair bets the small bet on fourth street",
		Holdings = Hands(
			Holes("3h 5h", "4c"),
			Holes("2d 3d", "Ah", fourth: "9d"),
			Holes("4h 5d", "Jh"),
			Holes("4s 5s", "Th"),
			Holes("2h 3c", "Kc", fourth: "Kd")),
		Actions =
		[
			SevenCardStudAction.Raise(
				HoldEmPosition.SmallBlind,
				SevenCardStudStreet.Third,
				raiseTo: SmallBet,
				potAfter: 40,
				upCards: Doors("4c", "Ah", "Jh", "Th", "Kc")),
			SevenCardStudAction.Fold(HoldEmPosition.BigBlind, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.UnderTheGun, SevenCardStudStreet.Third),
			SevenCardStudAction.Call(
				HoldEmPosition.Cutoff,
				SevenCardStudStreet.Third,
				callAmount: SmallBet,
				potAfter: 50,
				streetBet: SmallBet),
			SevenCardStudAction.Fold(HoldEmPosition.Button, SevenCardStudStreet.Third),
			SevenCardStudAction.Bet(
				HoldEmPosition.Cutoff,
				SevenCardStudStreet.Fourth,
				amount: SmallBet,
				potAfter: 60,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.Cutoff] = ["Kc", "Kd"],
					[HoldEmPosition.SmallBlind] = ["Ah", "9d"]
				}),
			SevenCardStudAction.Fold(HoldEmPosition.SmallBlind, SevenCardStudStreet.Fourth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Cutoff] = 35,
			[HoldEmPosition.SmallBlind] = -15,
			[HoldEmPosition.Button] = -10,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante
		},
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.Cutoff,
		AwardedAmount = 60,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     Both live doors are aces. The heart is left of the dealer, and the spade acts first.
	///     Fifth street is a 20 bet. The big blind's down card completes a diamond flush.
	/// </summary>
	public static SevenCardStudHandScript BestFiveShowdown { get; } = new()
	{
		Name = "Best five cards win at showdown",
		Holdings = Hands(
			Holes("3h 5h", "2c"),
			Holes("8s 7c", "Ah", fourth: "6h", fifth: "4h", sixth: "9h", seventh: "3s"),
			Holes("2d 5d", "As", fourth: "6d", fifth: "4d", sixth: "9d", seventh: "Kd"),
			Holes("4s 5s", "3c"),
			Holes("8d Jh", "Qc")),
		Actions =
		[
			SevenCardStudAction.Raise(
				HoldEmPosition.SmallBlind,
				SevenCardStudStreet.Third,
				raiseTo: SmallBet,
				potAfter: 40,
				upCards: Doors("2c", "Ah", "As", "3c", "Qc")),
			SevenCardStudAction.Call(
				HoldEmPosition.BigBlind,
				SevenCardStudStreet.Third,
				callAmount: SmallBet,
				potAfter: 50,
				streetBet: SmallBet),
			SevenCardStudAction.Fold(HoldEmPosition.UnderTheGun, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.Cutoff, SevenCardStudStreet.Third),
			SevenCardStudAction.Fold(HoldEmPosition.Button, SevenCardStudStreet.Third),
			SevenCardStudAction.Check(
				HoldEmPosition.BigBlind,
				SevenCardStudStreet.Fourth,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["As", "6d"],
					[HoldEmPosition.SmallBlind] = ["Ah", "6h"]
				}),
			SevenCardStudAction.Check(HoldEmPosition.SmallBlind, SevenCardStudStreet.Fourth),
			SevenCardStudAction.Bet(
				HoldEmPosition.BigBlind,
				SevenCardStudStreet.Fifth,
				amount: BigBet,
				potAfter: 70,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["As", "6d", "4d"],
					[HoldEmPosition.SmallBlind] = ["Ah", "6h", "4h"]
				}),
			SevenCardStudAction.Call(
				HoldEmPosition.SmallBlind,
				SevenCardStudStreet.Fifth,
				callAmount: BigBet,
				potAfter: 90),
			SevenCardStudAction.Check(HoldEmPosition.BigBlind, SevenCardStudStreet.Sixth),
			SevenCardStudAction.Check(HoldEmPosition.SmallBlind, SevenCardStudStreet.Sixth),
			SevenCardStudAction.Check(
				HoldEmPosition.BigBlind,
				SevenCardStudStreet.Seventh,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["As", "6d", "4d", "9d"],
					[HoldEmPosition.SmallBlind] = ["Ah", "6h", "4h", "9h"]
				}),
			SevenCardStudAction.Check(HoldEmPosition.SmallBlind, SevenCardStudStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 55,
			[HoldEmPosition.SmallBlind] = -35,
			[HoldEmPosition.Button] = -10,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 90,
		OpeningPot = OpeningPot,
		WonByFold = false
	};

	/// <summary>
	///     Three players reach sixth street. The under-the-gun pair of queens bets 20 and the others fold.
	/// </summary>
	public static SevenCardStudHandScript SixthStreetBigBet { get; } = new()
	{
		Name = "Sixth street is a big bet",
		Holdings = Hands(
			Holes("4h 5h", "Th"),
			Holes("6c 7h", "Kd", fourth: "2h", fifth: "5c", sixth: "8h"),
			Holes("4d 5d", "3d", fourth: "7d", fifth: "6s", sixth: "4s"),
			Holes("8c 9c", "Qd", fourth: "Qh", fifth: "9s", sixth: "Tc"),
			Holes("2s 3s", "Jh")),
		Actions =
		[
			SevenCardStudAction.Call(
				HoldEmPosition.UnderTheGun,
				SevenCardStudStreet.Third,
				callAmount: BringInAmount,
				potAfter: 35,
				streetBet: BringInAmount),
			SevenCardStudAction.Fold(HoldEmPosition.Cutoff, SevenCardStudStreet.Third, Doors(
				"Th", "Kd", "3d", "Qd", "Jh")),
			SevenCardStudAction.Fold(HoldEmPosition.Button, SevenCardStudStreet.Third),
			SevenCardStudAction.Call(
				HoldEmPosition.SmallBlind,
				SevenCardStudStreet.Third,
				callAmount: BringInAmount,
				potAfter: 40),
			SevenCardStudAction.Check(
				HoldEmPosition.UnderTheGun,
				SevenCardStudStreet.Fourth,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.UnderTheGun] = ["Qd", "Qh"],
					[HoldEmPosition.SmallBlind] = ["Kd", "2h"],
					[HoldEmPosition.BigBlind] = ["3d", "7d"]
				}),
			SevenCardStudAction.Check(HoldEmPosition.SmallBlind, SevenCardStudStreet.Fourth),
			SevenCardStudAction.Check(HoldEmPosition.BigBlind, SevenCardStudStreet.Fourth),
			SevenCardStudAction.Check(HoldEmPosition.UnderTheGun, SevenCardStudStreet.Fifth),
			SevenCardStudAction.Check(HoldEmPosition.SmallBlind, SevenCardStudStreet.Fifth),
			SevenCardStudAction.Check(HoldEmPosition.BigBlind, SevenCardStudStreet.Fifth),
			SevenCardStudAction.Bet(
				HoldEmPosition.UnderTheGun,
				SevenCardStudStreet.Sixth,
				amount: BigBet,
				potAfter: 60,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.UnderTheGun] = ["Qd", "Qh", "9s", "Tc"],
					[HoldEmPosition.SmallBlind] = ["Kd", "2h", "5c", "8h"],
					[HoldEmPosition.BigBlind] = ["3d", "7d", "6s", "4s"]
				}),
			SevenCardStudAction.Fold(HoldEmPosition.SmallBlind, SevenCardStudStreet.Sixth),
			SevenCardStudAction.Fold(HoldEmPosition.BigBlind, SevenCardStudStreet.Sixth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 30,
			[HoldEmPosition.SmallBlind] = -10,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 60,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static IReadOnlyList<SevenCardStudHandScript> Orbit { get; } =
	[
		SuitDecidesTheBringIn,
		CompleteTheBringIn,
		PairActsForTheSmallBet,
		BestFiveShowdown,
		SixthStreetBigBet
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

	private static Dictionary<HoldEmPosition, SevenCardStudHolding> Hands(
		SevenCardStudHolding button,
		SevenCardStudHolding smallBlind,
		SevenCardStudHolding bigBlind,
		SevenCardStudHolding underTheGun,
		SevenCardStudHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static SevenCardStudHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null)
	{
		var cards = hole.Split(' ');
		return new SevenCardStudHolding
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
