namespace CardGames.E2E.Infrastructure;

public enum RazzStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum RazzActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

/// <summary>
///     One Razz action. Bet and raise amounts are the street total the action panel submits.
/// </summary>
public sealed record RazzAction(
	HoldEmPosition Actor,
	RazzActionKind Kind,
	RazzStreet Street,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null)
{
	public static RazzAction Fold(
		HoldEmPosition actor,
		RazzStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(actor, RazzActionKind.Fold, street, UpCards: upCards);

	public static RazzAction Check(HoldEmPosition actor, RazzStreet street) =>
		new(actor, RazzActionKind.Check, street);

	public static RazzAction Call(
		HoldEmPosition actor,
		RazzStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, RazzActionKind.Call, street, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static RazzAction Bet(
		HoldEmPosition actor,
		RazzStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(actor, RazzActionKind.Bet, street, amount, PotAfter: potAfter, StreetBet: amount, UpCards: upCards);

	public static RazzAction Raise(
		HoldEmPosition actor,
		RazzStreet street,
		int raiseTo,
		int potAfter) =>
		new(actor, RazzActionKind.RaiseTo, street, raiseTo, PotAfter: potAfter, StreetBet: raiseTo);
}

/// <summary>
///     Cards a seat receives, in deal order. Seventh street is face down.
/// </summary>
public sealed record RazzHolding
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
///     A five-handed Razz deal. Positions are offsets from the dealer.
///     Nets are the stack change when the hand is over.
/// </summary>
public sealed record RazzHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, RazzHolding> Holdings { get; init; }

	public required IReadOnlyList<RazzAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }

	/// <summary>
	///     Showdown callout, such as "5-4-3-2-Ace". Absent when the hand is won by fold.
	/// </summary>
	public string? WinnerDescription { get; init; }
}

public sealed class RazzLedger
{
	private readonly int[] _stacks;

	public RazzLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(RazzHandScript script, int dealerSeat)
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
///     Five deals for one Razz sitting. Ante 5, bring-in 10, small bet 10, big bet 20.
///     The highest door brings in and ace is the lowest door. Later streets act from the best low board.
///     An open pair on fourth street uses the big bet.
/// </summary>
public static class RazzFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = 35;

	/// <summary>
	///     The spade king brings in ahead of the heart king. The ace door does not.
	///     Everyone else folds, and the bring-in wins the antes.
	/// </summary>
	public static RazzHandScript HighDoor { get; } = new()
	{
		Name = "High door brings in",
		Holdings = Hands(
			Holes("5s 6s", "Ks"),
			Holes("5h 6h", "Ah"),
			Holes("5d 6d", "9d"),
			Holes("5c 6c", "7c"),
			Holes("7d 8d", "Kh")),
		Actions =
		[
			RazzAction.Fold(HoldEmPosition.SmallBlind, RazzStreet.Third, Doors("Ks", "Ah", "9d", "7c", "Kh")),
			RazzAction.Fold(HoldEmPosition.BigBlind, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.UnderTheGun, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Cutoff, RazzStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.Button, winnerNet: 20),
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.Button,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     A suited A-2-3-4-5 is still a wheel. Fourth street has no open pair, so the bet is the small bet
	///     and the lower board acts first. Fifth street is the big bet.
	/// </summary>
	public static RazzHandScript SuitedWheel { get; } = new()
	{
		Name = "Suited wheel is still a wheel",
		Holdings = Hands(
			Holes("5s 8s", "Ks", fourth: "9c"),
			Holes("Ah 2h", "3h", fourth: "4h", fifth: "5h", sixth: "7s", seventh: "Qc"),
			Holes("Ad 2d", "6c", fourth: "4d", fifth: "3d", sixth: "9d", seventh: "Qs"),
			Holes("5c 8c", "9h"),
			Holes("5d 8d", "Th")),
		Actions =
		[
			RazzAction.Raise(
				HoldEmPosition.SmallBlind,
				RazzStreet.Third,
				raiseTo: SmallBet * 2,
				potAfter: 55),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Third, callAmount: SmallBet * 2, potAfter: 75, streetBet: SmallBet * 2),
			RazzAction.Fold(HoldEmPosition.UnderTheGun, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Cutoff, RazzStreet.Third),
			RazzAction.Call(HoldEmPosition.Button, RazzStreet.Third, callAmount: SmallBet, potAfter: 85),
			RazzAction.Bet(
				HoldEmPosition.SmallBlind,
				RazzStreet.Fourth,
				amount: SmallBet,
				potAfter: 95,
				upCards: Boards(
					(HoldEmPosition.SmallBlind, ["3h", "4h"]),
					(HoldEmPosition.BigBlind, ["6c", "4d"]),
					(HoldEmPosition.Button, ["Ks", "9c"]))),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Fourth, callAmount: SmallBet, potAfter: 105, streetBet: SmallBet),
			RazzAction.Fold(HoldEmPosition.Button, RazzStreet.Fourth),
			RazzAction.Bet(HoldEmPosition.SmallBlind, RazzStreet.Fifth, amount: BigBet, potAfter: 125),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Fifth, callAmount: BigBet, potAfter: 145),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Seventh),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 90,
			[HoldEmPosition.BigBlind] = -55,
			[HoldEmPosition.Button] = -25,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 145,
		OpeningPot = OpeningPot,
		WinnerDescription = "5-4-3-2-Ace"
	};

	/// <summary>
	///     A deuce pairs on fourth street. The unpaired seven-high board acts first and bets the big bet.
	///     The pair loses to 7-6-5-3-A at showdown.
	/// </summary>
	public static RazzHandScript OpenPair { get; } = new()
	{
		Name = "Open pair doubles fourth street",
		Holdings = Hands(
			Holes("4s 6s", "Ks"),
			Holes("Ah 5h", "7c", fourth: "3c", fifth: "6d", sixth: "8c", seventh: "9s"),
			Holes("Kd Qd", "2c", fourth: "2d", fifth: "2h", sixth: "Kc", seventh: "Js"),
			Holes("4c 5c", "9d"),
			Holes("4d 5d", "Td")),
		Actions =
		[
			RazzAction.Raise(HoldEmPosition.SmallBlind, RazzStreet.Third, raiseTo: SmallBet * 2, potAfter: 55),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Third, callAmount: SmallBet * 2, potAfter: 75, streetBet: SmallBet * 2),
			RazzAction.Fold(HoldEmPosition.UnderTheGun, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Cutoff, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Button, RazzStreet.Third),
			RazzAction.Bet(
				HoldEmPosition.SmallBlind,
				RazzStreet.Fourth,
				amount: BigBet,
				potAfter: 95,
				upCards: Boards(
					(HoldEmPosition.SmallBlind, ["7c", "3c"]),
					(HoldEmPosition.BigBlind, ["2c", "2d"]))),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Fourth, callAmount: BigBet, potAfter: 115),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Fifth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Fifth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Seventh),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 70,
			[HoldEmPosition.BigBlind] = -45,
			[HoldEmPosition.Button] = -15,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 115,
		OpeningPot = OpeningPot,
		WinnerDescription = "7-6-5-3-Ace"
	};

	/// <summary>
	///     Fifth street is a big-bet raise. A-2-3-4-6 beats 7-5-4-3-2. The seven-low is a straight and that does not help it.
	/// </summary>
	public static RazzHandScript FifthStreetRaise { get; } = new()
	{
		Name = "Fifth street raise",
		Holdings = Hands(
			Holes("9s 8s", "Ks"),
			Holes("Ah 2d", "4c", fourth: "3h", fifth: "6s", sixth: "Kd", seventh: "Qc"),
			Holes("2c 3c", "7d", fourth: "5d", fifth: "4d", sixth: "Kh", seventh: "Qh"),
			Holes("9c 8c", "Th"),
			Holes("9d 8d", "Ts")),
		Actions =
		[
			RazzAction.Raise(HoldEmPosition.SmallBlind, RazzStreet.Third, raiseTo: SmallBet * 2, potAfter: 55),
			RazzAction.Call(HoldEmPosition.BigBlind, RazzStreet.Third, callAmount: SmallBet * 2, potAfter: 75, streetBet: SmallBet * 2),
			RazzAction.Fold(HoldEmPosition.UnderTheGun, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Cutoff, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Button, RazzStreet.Third),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Fourth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Fourth),
			RazzAction.Bet(HoldEmPosition.SmallBlind, RazzStreet.Fifth, amount: BigBet, potAfter: 95),
			RazzAction.Raise(HoldEmPosition.BigBlind, RazzStreet.Fifth, raiseTo: BigBet * 2, potAfter: 135),
			RazzAction.Call(HoldEmPosition.SmallBlind, RazzStreet.Fifth, callAmount: BigBet, potAfter: 155),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Seventh),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 90,
			[HoldEmPosition.BigBlind] = -65,
			[HoldEmPosition.Button] = -15,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.Button,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 155,
		OpeningPot = OpeningPot,
		WinnerDescription = "6-4-3-2-Ace"
	};

	/// <summary>
	///     Check-down to seventh street. An eight-low beats kings that would win Seven Card Stud high.
	///     The ace door does not bring in.
	/// </summary>
	public static RazzHandScript HighCardsLose { get; } = new()
	{
		Name = "High cards lose",
		Holdings = Hands(
			Holes("7h 8s", "Ad"),
			Holes("2h 4h", "5c", fourth: "6d", fifth: "8c", sixth: "9s", seventh: "Qc"),
			Holes("Kh Kd", "Kc", fourth: "9c", fifth: "Jc", sixth: "Qh", seventh: "3s"),
			Holes("7c 8d", "9h"),
			Holes("7d 8h", "Td")),
		Actions =
		[
			RazzAction.Fold(HoldEmPosition.UnderTheGun, RazzStreet.Third, Doors("Ad", "5c", "Kc", "9h", "Td")),
			RazzAction.Fold(HoldEmPosition.Cutoff, RazzStreet.Third),
			RazzAction.Fold(HoldEmPosition.Button, RazzStreet.Third),
			RazzAction.Call(
				HoldEmPosition.SmallBlind,
				RazzStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Fourth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Fourth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Fifth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Fifth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Sixth),
			RazzAction.Check(HoldEmPosition.SmallBlind, RazzStreet.Seventh),
			RazzAction.Check(HoldEmPosition.BigBlind, RazzStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 30,
			[HoldEmPosition.BigBlind] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 45,
		OpeningPot = OpeningPot,
		WinnerDescription = "8-6-5-4-2"
	};

	public static IReadOnlyList<RazzHandScript> Orbit { get; } =
	[
		HighDoor,
		SuitedWheel,
		OpenPair,
		FifthStreetRaise,
		HighCardsLose
	];

	public static bool FoldedBefore(RazzHandScript script, HoldEmPosition position, RazzStreet street) =>
		script.Actions.Any(action =>
			action.Actor == position &&
			action.Kind == RazzActionKind.Fold &&
			action.Street < street);

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

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Boards(
		params (HoldEmPosition Position, string[] Cards)[] boards)
	{
		var map = new Dictionary<HoldEmPosition, IReadOnlyList<string>>();
		foreach (var (position, cards) in boards)
			map[position] = cards;

		return map;
	}

	private static Dictionary<HoldEmPosition, RazzHolding> Hands(
		RazzHolding button,
		RazzHolding smallBlind,
		RazzHolding bigBlind,
		RazzHolding underTheGun,
		RazzHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static RazzHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null)
	{
		var cards = hole.Split(' ');
		return new RazzHolding
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
