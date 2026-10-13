namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Klondike Playwright orbit. Blinds are 5 and 10.
///     The Klondike card is the face-down wild card dealt after the hole cards.
///     Positions are offsets from the dealer: button, small blind, big blind, under the gun, cutoff.
/// </summary>
public static class KlondikeFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static HoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h", "4h 5h", "6h 7h", "8h 9h", "Qh Kh"),
		KlondikeCard = "7d",
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
		KlondikeCard = "7s",
		Board = ["2s", "3s", "4s", "5s", "6s"],
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

	public static HoldEmHandScript WildStraightFlush { get; } = new()
	{
		Name = "Wild straight flush",
		HoleCards = Holes("Ah Ad", "2c 3c", "9h 8h", "4c 5c", "6c 7c"),
		KlondikeCard = "5s",
		Board = ["7h", "6h", "2d", "3d", "Kd"],
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
		WinnerDescription = "Straight flush to the Ten"
	};

	public static HoldEmHandScript FiveOfAKind { get; } = new()
	{
		Name = "Five of a kind",
		HoleCards = Holes("Ks Kd", "5s 6s", "As Ah", "7c 2d", "5h 6h"),
		KlondikeCard = "4h",
		Board = ["Kc", "Kh", "8d", "7s", "3c"],
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
		WinnerDescription = "Five of a kind, Kings"
	};

	public static HoldEmHandScript OnlyTheKlondikeCardIsWild { get; } = new()
	{
		Name = "Only the Klondike card is wild",
		HoleCards = Holes("Ah 9d", "4d 5d", "2h 4c", "6d 5h", "Qc Jd"),
		KlondikeCard = "2c",
		Board = ["Ks", "7d", "8c", "3h", "9c"],
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
		WonByFold = false,
		WinnerDescription = "Three of a kind, Nines"
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		WildStraightFlush,
		FiveOfAKind,
		OnlyTheKlondikeCardIsWild
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
