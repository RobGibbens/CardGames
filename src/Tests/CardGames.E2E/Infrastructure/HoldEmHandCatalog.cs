namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Seat offsets from the dealer at a six-handed table.
/// </summary>
public enum HoldEmPosition
{
	Button = 0,
	SmallBlind = 1,
	BigBlind = 2,
	UnderTheGun = 3,
	Middle = 4,
	Cutoff = 5
}

public enum HoldEmActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	AllIn
}

/// <summary>
///     One betting action. Amounts for <see cref="HoldEmActionKind.Bet" /> and
///     <see cref="HoldEmActionKind.RaiseTo" /> are the street total the action panel submits.
/// </summary>
public sealed record HoldEmAction(
	HoldEmPosition Actor,
	HoldEmActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	int? CommunityCount = null)
{
	public static HoldEmAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, HoldEmActionKind.Fold, PotAfter: potAfter);

	public static HoldEmAction Check(HoldEmPosition actor, int? communityCount = null) =>
		new(actor, HoldEmActionKind.Check, CommunityCount: communityCount);

	public static HoldEmAction Call(HoldEmPosition actor, int callAmount, int potAfter, int? streetBet = null) =>
		new(actor, HoldEmActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static HoldEmAction Bet(HoldEmPosition actor, int amount, int potAfter, int? communityCount = null) =>
		new(actor, HoldEmActionKind.Bet, amount, potAfter, StreetBet: amount, CommunityCount: communityCount);

	public static HoldEmAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, HoldEmActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static HoldEmAction AllIn(HoldEmPosition actor, int potAfter, int streetBet) =>
		new(actor, HoldEmActionKind.AllIn, PotAfter: potAfter, StreetBet: streetBet);
}

/// <summary>
///     A fully specified six-handed Hold'em deal and the betting line played through the UI.
///     Nets are the change in each stack when the hand is over. <see cref="AwardedAmount" /> is the
///     gross pot the showdown overlay shows for the primary winner.
/// </summary>
public sealed record HoldEmHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<string> Board { get; init; }

	public required IReadOnlyList<HoldEmAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }

	/// <summary>
	///     Showdown callout text. Set for hands that reach an evaluated showdown.
	/// </summary>
	public string? WinnerDescription { get; init; }

	/// <summary>
	///     Face-down wild card dealt after the hole cards. Set for Klondike scripts.
	/// </summary>
	public string? KlondikeCard { get; init; }
}

public static class HoldEmSeats
{
	public const int Count = 6;

	public static int Index(int dealerSeat, HoldEmPosition position) =>
		Index(Count, dealerSeat, position);

	public static int Index(int seatCount, int dealerSeat, HoldEmPosition position) =>
		(dealerSeat + Offset(seatCount, position)) % seatCount;

	/// <summary>
	///     Seat offset from the dealer. The cutoff is the last seat, so a five-handed cutoff
	///     is offset 4 and a six-handed cutoff stays offset 5. Middle exists only at six-max.
	/// </summary>
	public static int Offset(int seatCount, HoldEmPosition position) => position switch
	{
		HoldEmPosition.Cutoff => seatCount - 1,
		HoldEmPosition.Middle when seatCount == Count => (int)HoldEmPosition.Middle,
		HoldEmPosition.Button => (int)HoldEmPosition.Button,
		HoldEmPosition.SmallBlind => (int)HoldEmPosition.SmallBlind,
		HoldEmPosition.BigBlind => (int)HoldEmPosition.BigBlind,
		HoldEmPosition.UnderTheGun => (int)HoldEmPosition.UnderTheGun,
		_ => throw new InvalidOperationException(
			$"{position} is not a seat at a {seatCount}-handed table.")
	};
}

/// <summary>
///     Running table stacks. Each hand's nets must conserve the chips that were there at the start.
/// </summary>
public sealed class HoldEmLedger
{
	private readonly int[] _stacks;

	public HoldEmLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(HoldEmHandScript script, int dealerSeat) =>
		Apply(script.Name, script.Nets, dealerSeat);

	public void Apply(string handName, IReadOnlyDictionary<HoldEmPosition, int> nets, int dealerSeat)
	{
		foreach (var (position, net) in nets)
			_stacks[HoldEmSeats.Index(_stacks.Length, dealerSeat, position)] += net;

		var sum = _stacks.Sum();
		if (sum != StartingSum)
		{
			throw new InvalidOperationException(
				$"Hand '{handName}' changed the table chip total from {StartingSum} to {sum}.");
		}
	}
}

/// <summary>
///     Known deals for the six-player Playwright suite. Blinds are 5 and 10.
/// </summary>
public static class HoldEmHandCatalog
{
	public static HoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h", "4h 5h", "6h 7h", "8h 9h", "Th Jh", "Qh Kh"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 5,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 15,
		WonByFold = true
	};

	public static HoldEmHandScript OpenRaiseAndFlopBet { get; } = new()
	{
		Name = "Open raise and flop bet",
		HoleCards = Holes("2c 3c", "4c 5c", "6c 7c", "Ah Ad", "Ks Qs", "8c 9c"),
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			HoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 30, potAfter: 75, streetBet: 30),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			// Calling closes preflop, so the seat bet is swept in the same update as the pot.
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 95),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Bet(HoldEmPosition.UnderTheGun, amount: 40, potAfter: 135),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.BigBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 65,
			[HoldEmPosition.Middle] = -30,
			[HoldEmPosition.BigBlind] = -30,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 135,
		WonByFold = true
	};

	public static HoldEmHandScript Checkdown { get; } = new()
	{
		Name = "Checkdown",
		HoleCards = Holes("As Ac", "3d 4d", "Ks Qs", "3h 4h", "5h 6h", "Qc Jd"),
		Board = ["Ah", "Kd", "7c", "2s", "9h"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 15,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 25,
		WonByFold = false
	};

	public static HoldEmHandScript RiverRaise { get; } = new()
	{
		Name = "River raise",
		HoleCards = Holes("As Ac", "5s 6s", "7c 2d", "5h 6h", "5d 6d", "5c 6c"),
		Board = ["Ah", "9c", "4d", "3h", "8s"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 4),
			// Calling closes the turn, so the seat bet is swept in the same update as the pot.
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Bet(HoldEmPosition.Button, amount: 40, potAfter: 105),
			HoldEmAction.Raise(HoldEmPosition.BigBlind, raiseTo: 100, potAfter: 205),
			// Calling closes the river and ends the hand, so the seat bet never stays up.
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 60, potAfter: 265)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 135,
			[HoldEmPosition.BigBlind] = -130,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 265,
		WonByFold = false
	};

	public static HoldEmHandScript SplitPot { get; } = new()
	{
		Name = "Split pot",
		HoleCards = Holes("6c 7d", "8c 9d", "4c 5d", "2c 3d", "6d 7s", "8d 9s"),
		Board = ["Ah", "Kh", "Qh", "Jh", "Th"],
		Actions =
		[
			HoldEmAction.Call(HoldEmPosition.UnderTheGun, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 3,
			[HoldEmPosition.UnderTheGun] = 2,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 13,
		WonByFold = false
	};

	public static HoldEmHandScript BigBlindRaisesLimpers { get; } = new()
	{
		Name = "Big blind raises limpers",
		HoleCards = Holes("2h 7d", "3h 8d", "Ah Kh", "4h 9d", "5h Td", "6h Jd"),
		Board = ["2c", "3c", "4c", "5c", "6c"],
		Actions =
		[
			HoldEmAction.Call(HoldEmPosition.UnderTheGun, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 10, potAfter: 35, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Call(HoldEmPosition.SmallBlind, callAmount: 5, potAfter: 40, streetBet: 10),
			HoldEmAction.Raise(HoldEmPosition.BigBlind, raiseTo: 40, potAfter: 70),
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 30, potAfter: 100, streetBet: 40),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 120, communityCount: 3),
			HoldEmAction.Fold(HoldEmPosition.Middle)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 60,
			[HoldEmPosition.Middle] = -40,
			[HoldEmPosition.UnderTheGun] = -10,
			[HoldEmPosition.SmallBlind] = -10
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 120,
		WonByFold = true
	};

	/// <summary>
	///     Fixed short stacks: big blind 200, under the gun 80, everyone else 1,000.
	///     The two players who still have chips check the turn and the river so the
	///     scripted board is complete before showdown.
	/// </summary>
	public static HoldEmHandScript SidePot { get; } = new()
	{
		Name = "Side pot",
		HoleCards = Holes("5h 6h", "5d 6d", "7c 2d", "Ah Ad", "Kh Kd", "5c 6c"),
		Board = ["As", "9c", "4d", "3h", "8s"],
		Actions =
		[
			HoldEmAction.AllIn(HoldEmPosition.UnderTheGun, potAfter: 95, streetBet: 80),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 80, potAfter: 175, streetBet: 80),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			// These calls close the street, so the seat bet is cleared in the same update as the pot.
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 70, potAfter: 245),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 50, potAfter: 295, communityCount: 3),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 50, potAfter: 345),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.Middle),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.Middle)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 165,
			[HoldEmPosition.Middle] = -30,
			[HoldEmPosition.BigBlind] = -130,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 245,
		WonByFold = false
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		Checkdown,
		RiverRaise,
		SplitPot,
		BigBlindRaisesLimpers
	];

	/// <summary>
	///     SB 40, UTG 80, BB and middle call 80 then the big blind bets 100 and is called.
	///     Aces take the 160 main, kings the 120 side, queens the 200 side.
	/// </summary>
	public static HoldEmHandScript ThreeTiers { get; } = new()
	{
		Name = "Three contribution tiers",
		HoleCards = Holes("6c 7d", "Ah Ad", "Qh Qd", "Kh Kd", "Jh Jd", "7c 8c"),
		Board = ["2c", "3d", "4h", "5s", "9c"],
		Actions =
		[
			HoldEmAction.AllIn(HoldEmPosition.UnderTheGun, potAfter: 95, streetBet: 80),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 80, potAfter: 175, streetBet: 80),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.AllIn(HoldEmPosition.SmallBlind, potAfter: 210, streetBet: 40),
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 70, potAfter: 280),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 100, potAfter: 380, communityCount: 3),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 100, potAfter: 480, streetBet: 100),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.Middle),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.Middle)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 120,
			[HoldEmPosition.UnderTheGun] = 40,
			[HoldEmPosition.BigBlind] = 20,
			[HoldEmPosition.Middle] = -180
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 200,
		WonByFold = false
	};

	/// <summary>Blinds fold to an open raise. Their chips stay in the pot.</summary>
	public static HoldEmHandScript FoldedChipsRemain { get; } = new()
	{
		Name = "Folded chips remain in the pot",
		HoleCards = Holes("2h 3h", "4h 5h", "6h 7h", "Ah Ad", "Kc Qd", "8c 9c"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
		Actions =
		[
			HoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Fold(HoldEmPosition.BigBlind, potAfter: 45)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 15,
			[HoldEmPosition.SmallBlind] = -5,
			[HoldEmPosition.BigBlind] = -10
		},
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 45,
		WonByFold = true
	};

	/// <summary>
	///     Two short aces chop the 200 main pot. Kings win the 200 side pot.
	/// </summary>
	public static HoldEmHandScript SplitMainDifferentSideWinner { get; } = new()
	{
		Name = "Split main and a different side winner",
		HoleCards = Holes("6c 7d", "Ah Ad", "Kh Kd", "As Ac", "Qh Qd", "7c 8c"),
		Board = ["2c", "3d", "4h", "5s", "9c"],
		Actions =
		[
			HoldEmAction.AllIn(HoldEmPosition.UnderTheGun, potAfter: 65, streetBet: 50),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 50, potAfter: 115, streetBet: 50),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.AllIn(HoldEmPosition.SmallBlind, potAfter: 160, streetBet: 50),
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 40, potAfter: 200),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 100, potAfter: 300, communityCount: 3),
			HoldEmAction.Call(HoldEmPosition.Middle, callAmount: 100, potAfter: 400, streetBet: 100),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.Middle),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.Middle)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 50,
			[HoldEmPosition.UnderTheGun] = 50,
			[HoldEmPosition.BigBlind] = 50,
			[HoldEmPosition.Middle] = -150
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 200,
		WonByFold = false
	};

	/// <summary>The big blind can only call 100 of an open shove. 900 comes back uncalled.</summary>
	public static HoldEmHandScript UncalledRefund { get; } = new()
	{
		Name = "Uncalled shove refund",
		HoleCards = Holes("5h 6h", "5d 6d", "Ah Ad", "7c 2d", "Th Jh", "Qc Qd"),
		Board = ["As", "9c", "4d", "3h", "8s"],
		Actions =
		[
			HoldEmAction.AllIn(HoldEmPosition.UnderTheGun, potAfter: 1015, streetBet: 1000),
			HoldEmAction.Fold(HoldEmPosition.Middle),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.AllIn(HoldEmPosition.BigBlind, potAfter: 1105, streetBet: 100)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 105,
			[HoldEmPosition.UnderTheGun] = -100,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 205,
		WonByFold = false
	};

	public static IEnumerable<HoldEmHandScript> All =>
		Orbit.Append(SidePot).Append(ThreeTiers).Append(FoldedChipsRemain)
			.Append(SplitMainDifferentSideWinner).Append(UncalledRefund);

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Holes(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string middle,
		string cutoff)
	{
		return new Dictionary<HoldEmPosition, IReadOnlyList<string>>
		{
			[HoldEmPosition.Button] = Pair(button),
			[HoldEmPosition.SmallBlind] = Pair(smallBlind),
			[HoldEmPosition.BigBlind] = Pair(bigBlind),
			[HoldEmPosition.UnderTheGun] = Pair(underTheGun),
			[HoldEmPosition.Middle] = Pair(middle),
			[HoldEmPosition.Cutoff] = Pair(cutoff)
		};
	}

	private static IReadOnlyList<string> Pair(string cards) => cards.Split(' ');
}
