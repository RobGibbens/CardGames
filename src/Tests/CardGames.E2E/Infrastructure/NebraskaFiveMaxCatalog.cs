namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Nebraska Playwright orbit. Blinds are 5 and 10.
///     Positions are offsets from the dealer: button, small blind, big blind, under the gun, cutoff.
///     Each player is dealt five hole cards. A showdown hand uses exactly three of them with exactly
///     two community cards.
/// </summary>
public static class NebraskaFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static HoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes(
			"2h 3h 4h 5h 6h",
			"7h 8h 9h Th Jh",
			"Qh Kh 2d 3d 4d",
			"5d 6d 7d 8d 9d",
			"Td Jd Qd Kd Ad"),
		Board = ["2c", "3c", "4c", "5c", "6c"],
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
		HoleCards = Holes(
			"2s 3s 4s 5s 6s",
			"7s 8s 9s Ts Js",
			"Qs Ks 2c 3c 4c",
			"5c 6c 7c 8c 9c",
			"Tc Jc Qc Kc Ac"),
		Board = ["2h", "3h", "4h", "5h", "6h"],
		Actions =
		[
			HoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
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

	public static HoldEmHandScript BoardRoyalDoesNotPlay { get; } = new()
	{
		Name = "Board royal does not play",
		HoleCards = Holes(
			"As Ks Qs 2c 3d",
			"9d 8s 7s 6s 5s",
			"2h 3h 4h 5c 6c",
			"7c 8c 9c Td 4d",
			"4c 5d 6d 7d 8d"),
		Board = ["Ah", "Kh", "Qh", "Jh", "Th"],
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
			[HoldEmPosition.BigBlind] = 15,
			[HoldEmPosition.Button] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 25,
		WonByFold = false,
		WinnerDescription = "Ace high flush"
	};

	public static HoldEmHandScript RiverRaise { get; } = new()
	{
		Name = "River raise",
		HoleCards = Holes(
			"Ac Ad Ah 2d 7c",
			"Qh 2s 3s 4s 5s",
			"Ks Kc Kh Qd Jd",
			"2h 5h 6h 6d 9d",
			"7h 8h 9h Th Jh"),
		Board = ["As", "9c", "4d", "3h", "8s"],
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
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Bet(HoldEmPosition.Button, amount: 40, potAfter: 105),
			HoldEmAction.Raise(HoldEmPosition.BigBlind, raiseTo: 100, potAfter: 205),
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
		WinnerDescription = "Four of a kind, Aces"
	};

	public static HoldEmHandScript SplitStraight { get; } = new()
	{
		Name = "Split straight",
		HoleCards = Holes(
			"As Ks Qs 2c 3d",
			"4d 4s 3c 3s 9d",
			"Ad Kd Qd 4c 5d",
			"6c 7c 8c 6d 7d",
			"5c 5s 6s 7s 8s"),
		Board = ["Jh", "Th", "9c", "8d", "2h"],
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
			[HoldEmPosition.BigBlind] = 3,
			[HoldEmPosition.Button] = 2,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 13,
		WonByFold = false,
		WinnerDescription = "Straight to the Ace"
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		BoardRoyalDoesNotPlay,
		RiverRaise,
		SplitStraight
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
			[HoldEmPosition.Button] = Five(button),
			[HoldEmPosition.SmallBlind] = Five(smallBlind),
			[HoldEmPosition.BigBlind] = Five(bigBlind),
			[HoldEmPosition.UnderTheGun] = Five(underTheGun),
			[HoldEmPosition.Cutoff] = Five(cutoff)
		};
	}

	private static IReadOnlyList<string> Five(string cards) => cards.Split(' ');
}
