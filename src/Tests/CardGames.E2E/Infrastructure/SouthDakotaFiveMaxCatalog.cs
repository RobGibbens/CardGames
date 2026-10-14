namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player South Dakota Playwright orbit. Blinds are 5 and 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     Each player has five hole cards. The board is a two-card flop and a turn.
///     Showdown uses exactly three hole cards and two community cards.
/// </summary>
public static class SouthDakotaFiveMaxCatalog
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
		Board = ["2s", "3s", "4s"],
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

	public static HoldEmHandScript TwoCardFlopBet { get; } = new()
	{
		Name = "Two-card flop bet",
		HoleCards = Holes(
			"2c 3c 4c 5c 6c",
			"7c 8c 9c Tc Jc",
			"2d 3d 4d 5d 6d",
			"Ah Ad 5h 6h 7h",
			"8h 9h Th Jh Qh"),
		Board = ["2s", "3s", "4s"],
		Actions =
		[
			HoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Fold(HoldEmPosition.Button),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 65),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 2),
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

	public static HoldEmHandScript RoyalTrapCheckdown { get; } = new()
	{
		Name = "Checkdown, royal trap",
		HoleCards = Holes(
			"Ah Kh 2c 3c 4c",
			"6c 7c 8c 9c Tc",
			"2d 2h 2s 3d 4d",
			"5h 6h 7h 8h 4h",
			"5d 6d 7d 7s 8s"),
		Board = ["Qh", "Jh", "Th"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 2),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
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
		WinnerDescription = "Three of a kind, Deuces"
	};

	public static HoldEmHandScript TurnBetTrips { get; } = new()
	{
		Name = "Turn bet, three aces",
		HoleCards = Holes(
			"5s 6s 7s 8s 9s",
			"2h 3h 6h 7h 8h",
			"Ac Ad Ah Kc Qc",
			"8c 9c Tc Jc 4s",
			"Td Jd Qd Kd 5d"),
		Board = ["2c", "7d", "Kh"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 2),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 3),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 35,
			[HoldEmPosition.Button] = -30,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 65,
		WonByFold = false,
		WinnerDescription = "Three of a kind, Aces"
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		TwoCardFlopBet,
		RoyalTrapCheckdown,
		TurnBetTrips
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

	private static IReadOnlyList<string> Cards(string cards) => cards.Split(' ');
}
