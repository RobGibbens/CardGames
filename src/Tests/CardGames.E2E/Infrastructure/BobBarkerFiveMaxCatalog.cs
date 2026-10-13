namespace CardGames.E2E.Infrastructure;

/// <summary>
///     A fully specified five-handed Bob Barker deal. Nets are the change in each stack when the
///     hand is over. <see cref="AwardedAmount" /> is the gross amount the showdown overlay shows
///     for the primary winner. <see cref="ShowcaseWinner" /> receives the showcase half, which
///     rolls into the Omaha winner when nobody is at or under the dealer target.
/// </summary>
public sealed record BobBarkerHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, string> Showcases { get; init; }

	public required string DealerCard { get; init; }

	public required IReadOnlyList<string> Board { get; init; }

	public required IReadOnlyList<HoldEmAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }

	public HoldEmPosition? ShowcaseWinner { get; init; }

	public bool ShowcaseRolledIntoHighHand { get; init; }
}

/// <summary>
///     Running table stacks. Each hand's nets must conserve the chips that were there at the start.
/// </summary>
public sealed class BobBarkerLedger
{
	private readonly int[] _stacks;

	public BobBarkerLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(BobBarkerHandScript script, int dealerSeat)
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
///     Known deals for the five-player Bob Barker Playwright orbit. Blinds are 5 and 10.
///     Positions are offsets from the dealer: button, small blind, big blind, under the gun, cutoff.
/// </summary>
public static class BobBarkerFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static BobBarkerHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes(
			"2h 3h 4h 5h 6h",
			"7h 8h 9h Th Jh",
			"Qh Kh 2d 3d 4d",
			"5d 6d 7d 8d 9d",
			"Td Jd Qd Kd Ad"),
		Showcases = Picks("2h", "7h", "Qh", "5d", "Td"),
		DealerCard = "2c",
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
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

	public static BobBarkerHandScript OpenRaiseAndFlopBet { get; } = new()
	{
		Name = "Open raise and flop bet",
		HoleCards = Holes(
			"2h 3h 4h 5h 6h",
			"7h 8h 9h Th Jh",
			"Qh Kh Ah 2d 3d",
			"4d 5d 6d 7d 8d",
			"9d Td Jd Qd Kd"),
		Showcases = Picks("2h", "7h", "Ah", "4d", "9d"),
		DealerCard = "2c",
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			HoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			// Calling closes preflop, so the seat bet is swept in the same update as the pot.
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 65),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Bet(HoldEmPosition.UnderTheGun, amount: 40, potAfter: 105),
			HoldEmAction.Fold(HoldEmPosition.BigBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 35,
			[HoldEmPosition.BigBlind] = -30,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 105,
		WonByFold = true
	};

	public static BobBarkerHandScript CheckdownSplit { get; } = new()
	{
		Name = "Checkdown split",
		HoleCards = Holes(
			"As Ac 2c 3c Td",
			"5h 6h 7h 8h 9h",
			"Qd 8d 6d 5d 4d",
			"5c 6c 7d 8c 9c",
			"Jh Jc Js 3d 3h"),
		Showcases = Picks("Td", "5h", "Qd", "5c", "Jh"),
		DealerCard = "Ks",
		Board = ["Ah", "7c", "4s", "9s", "2s"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
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
			[HoldEmPosition.Button] = 3,
			[HoldEmPosition.BigBlind] = 2,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 13,
		WonByFold = false,
		ShowcaseWinner = HoldEmPosition.BigBlind
	};

	public static BobBarkerHandScript RiverRaiseNoQualifier { get; } = new()
	{
		Name = "River raise, no qualifier",
		HoleCards = Holes(
			"As Ac Kd 3c 4c",
			"5d 6d 7d 8d 9d",
			"Qh 2d 2h Jc Js",
			"5s 6s 7s 8c 9h",
			"Th Ts Tc Td 6c"),
		Showcases = Picks("Kd", "5d", "Qh", "5s", "Th"),
		DealerCard = "3s",
		Board = ["Ah", "9c", "4d", "7h", "8s"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
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
		WonByFold = false,
		ShowcaseWinner = HoldEmPosition.Button,
		ShowcaseRolledIntoHighHand = true
	};

	public static BobBarkerHandScript AceDealerTarget { get; } = new()
	{
		Name = "Ace dealer target",
		HoleCards = Holes(
			"Ks Kc Kd 2c 3c",
			"5c 6c 7d 8d 9c",
			"Ad Qd Jd Td 5h",
			"5s 6s 7s 8s 9s",
			"4c 4d 4h 6h 7h"),
		Showcases = Picks("2c", "5c", "Ad", "5s", "4c"),
		DealerCard = "Ah",
		Board = ["Kh", "Qc", "3s", "2h", "6d"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
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
			[HoldEmPosition.Button] = 3,
			[HoldEmPosition.BigBlind] = 2,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 13,
		WonByFold = false,
		ShowcaseWinner = HoldEmPosition.BigBlind
	};

	public static IReadOnlyList<BobBarkerHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		CheckdownSplit,
		RiverRaiseNoQualifier,
		AceDealerTarget
	];

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Holes(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff)
	{
		return new Dictionary<HoldEmPosition, IReadOnlyList<string>>
		{
			[HoldEmPosition.Button] = Cards(button),
			[HoldEmPosition.SmallBlind] = Cards(smallBlind),
			[HoldEmPosition.BigBlind] = Cards(bigBlind),
			[HoldEmPosition.UnderTheGun] = Cards(underTheGun),
			[HoldEmPosition.Cutoff] = Cards(cutoff)
		};
	}

	private static Dictionary<HoldEmPosition, string> Picks(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff)
	{
		return new Dictionary<HoldEmPosition, string>
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};
	}

	private static IReadOnlyList<string> Cards(string cards) => cards.Split(' ');
}
