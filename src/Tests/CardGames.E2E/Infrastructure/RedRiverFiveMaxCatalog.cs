namespace CardGames.E2E.Infrastructure;

/// <summary>
///     Known deals for the five-player Red River Playwright orbit. Blinds are 5 and 10.
///     Positions are offsets from the dealer: button, small blind, big blind, under the gun, cutoff.
///     A heart or diamond river includes the bonus card as the sixth board card.
/// </summary>
public static class RedRiverFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static HoldEmHandScript BlackRiverCheckdown { get; } = new()
	{
		Name = "Black river stays five cards",
		HoleCards = Holes("As Ac", "3d 4d", "Ks Qs", "5h 6h", "8d 4c"),
		Board = ["Ah", "Kd", "7c", "2s", "9c"],
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

	public static HoldEmHandScript HeartRiverBonus { get; } = new()
	{
		Name = "Heart river deals the bonus before betting",
		HoleCards = Holes("As Ad", "3d 5d", "Qd Qc", "6c 8c", "5c 6s"),
		Board = ["2c", "7d", "9s", "4h", "Kh", "Qh"],
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
			// The queen of hearts is already on the board, so the big blind has trip queens.
			HoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 6),
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
		WonByFold = false
	};

	public static HoldEmHandScript DiamondRiverBet { get; } = new()
	{
		Name = "Diamond river bets once",
		HoleCards = Holes("Js Jc", "4c 6d", "2d 3d", "7c 8c", "9c 7s"),
		Board = ["8d", "3c", "5s", "2h", "Td", "9d"],
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
			HoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 6),
			HoldEmAction.Fold(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 15,
			[HoldEmPosition.Button] = -10,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 45,
		WonByFold = true
	};

	/// <summary>
	///     Played fourth, with the button on seat 3. That seat holds 1,010 after the earlier hands,
	///     so the shove is 1,010 and the big blind calls 1,000. The big blind holds Jd 6c. The bonus
	///     seven of hearts completes a jack-high straight. Those hole cards are offsuit, so the heart
	///     river does not make a flush that would win without the bonus. Pocket aces win if the sixth
	///     card is not dealt.
	/// </summary>
	public static HoldEmHandScript AllInRunoutToHeart { get; } = new()
	{
		Name = "All-in runout onto a heart",
		HoleCards = Holes("As Ac", "3d 4d", "Jd 6c", "6d 7c", "4s 5c"),
		Board = ["Th", "9h", "8d", "2c", "5h", "7h"],
		Actions =
		[
			HoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			HoldEmAction.Fold(HoldEmPosition.Cutoff),
			HoldEmAction.AllIn(HoldEmPosition.Button, potAfter: 1_025, streetBet: 1_010),
			HoldEmAction.Fold(HoldEmPosition.SmallBlind),
			HoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 1_000, potAfter: 2_025)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 1_015,
			[HoldEmPosition.Button] = -1_010,
			[HoldEmPosition.SmallBlind] = -5
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 2_025,
		WonByFold = false
	};

	public static IReadOnlyList<HoldEmHandScript> Orbit { get; } =
	[
		BlackRiverCheckdown,
		HeartRiverBonus,
		DiamondRiverBet,
		AllInRunoutToHeart
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
