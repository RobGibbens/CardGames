namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Omaha Playwright orbit. Blinds are 5 and 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     Each player has four hole cards. A made hand uses exactly two of them and three board cards.
/// </summary>
public static class OmahaFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static HoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h 4h 5h", "6h 7h 8h 9h", "Jh Qh Kh Ah", "8c 9c Tc Jc", "2c 3c 6c 7c"),
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
		HoleCards = Holes("2c 3c 4c 5c", "6c 7c 8c 9c", "4h 5h 6h 7h", "Ah Ad 2s 3s", "8d 9d Td Jd"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
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

	/// <summary>
	///     The board is four kings. Omaha cannot play those quads. The button's two aces make
	///     kings full of aces; the big blind's single ace makes only three kings. Hold'em plays
	///     the ace kicker with all four kings and chops.
	/// </summary>
	public static HoldEmHandScript BoardQuadsAreAFullHouse { get; } = new()
	{
		Name = "Board quads are a full house",
		HoleCards = Holes("As Ad 2c 3d", "7c 8c 9c Tc", "Ah 4c 6d 9h", "7d 8d Qd Td", "7s 8s Qs Ts"),
		Board = ["Ks", "Kd", "Kh", "Kc", "5h"],
		Actions = Checkdown(),
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 15,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 25,
		WonByFold = false,
		WinnerDescription = "Full house, Kings full of Aces"
	};

	/// <summary>
	///     The button's two hearts plus the three hearts on the board are an ace-high flush.
	///     The big blind's four diamonds cannot use that flush.
	/// </summary>
	public static HoldEmHandScript TwoHoleCardsMakeTheFlush { get; } = new()
	{
		Name = "Two hole cards make the flush",
		HoleCards = Holes("Ah Kh Qc Jc", "7c 8c 9c Tc", "Ad Kd Qd Jd", "7d 8d 9d Td", "7s 8s 9s Ts"),
		Board = ["2h", "3h", "4h", "5s", "6c"],
		Actions = Checkdown(),
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 15,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 25,
		WonByFold = false,
		WinnerDescription = "Ace high flush"
	};

	/// <summary>
	///     Under the gun and the button play the same jack-high straight. The odd chip goes to
	///     under the gun, the first winner clockwise from the button.
	/// </summary>
	public static HoldEmHandScript SplitStraight { get; } = new()
	{
		Name = "Split straight",
		HoleCards = Holes("8h 7c 4d 5d", "2h 3h 4h 5h", "Qs Qc 6h 6d", "8d 7h 4c 5c", "2c 3c 4s 5s"),
		Board = ["9c", "Td", "Jh", "2s", "3d"],
		Actions =
		[
			HoldEmAction.Call(HoldEmPosition.UnderTheGun, callAmount: 10, potAfter: 25, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 35, streetBet: 10),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun),
			HoldEmAction.Check(HoldEmPosition.Button),
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			HoldEmAction.Check(HoldEmPosition.UnderTheGun),
			HoldEmAction.Check(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 8,
			[HoldEmPosition.Button] = 7,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 18,
		WonByFold = false,
		WinnerDescription = "Straight to the Jack"
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		OpenRaiseAndFlopBet,
		BoardQuadsAreAFullHouse,
		TwoHoleCardsMakeTheFlush,
		SplitStraight
	];

	private static IReadOnlyList<HoldEmAction> Checkdown() =>
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
