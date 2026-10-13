namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Playwright orbit. Blinds are 5 and 10.
///     Positions are offsets from the dealer: button, small blind, big blind, under the gun, cutoff.
/// </summary>
public static class HoldEmFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static HoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h", "4h 5h", "6h 7h", "8h 9h", "Qh Kh"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
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

	public static HoldEmHandScript OpenRaiseAndFlopBet { get; } = new()
	{
		Name = "Open raise and flop bet",
		HoleCards = Holes("2c 3c", "4c 5c", "6c 7c", "Ah Ad", "8c 9c"),
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

	public static HoldEmHandScript Checkdown { get; } = new()
	{
		Name = "Checkdown",
		HoleCards = Holes("As Ac", "3d 4d", "Ks Qs", "3h 4h", "Qc Jd"),
		Board = ["Ah", "Kd", "7c", "2s", "9h"],
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
		HoleCards = Holes("As Ac", "5s 6s", "7c 2d", "5h 6h", "5c 6c"),
		Board = ["Ah", "9c", "4d", "3h", "8s"],
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
		WonByFold = false
	};

	public static HoldEmHandScript SplitPot { get; } = new()
	{
		Name = "Split pot",
		HoleCards = Holes("6c 7d", "8c 9d", "4c 5d", "2c 3d", "8d 9s"),
		Board = ["Ah", "Kh", "Qh", "Jh", "Th"],
		Actions =
		[
			HoldEmAction.Call(HoldEmPosition.UnderTheGun, callAmount: 10, potAfter: 25, streetBet: 10),
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

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		Checkdown,
		RiverRaise,
		SplitPot
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
			[HoldEmPosition.Button] = Pair(button),
			[HoldEmPosition.SmallBlind] = Pair(smallBlind),
			[HoldEmPosition.BigBlind] = Pair(bigBlind),
			[HoldEmPosition.UnderTheGun] = Pair(underTheGun),
			[HoldEmPosition.Cutoff] = Pair(cutoff)
		};
	}

	private static IReadOnlyList<string> Pair(string cards) => cards.Split(' ');
}
