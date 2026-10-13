namespace CardGames.E2E.Infrastructure;

public enum CrazyPineappleActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	Discard
}

/// <summary>
///     One betting or discard action. <see cref="Card" /> is the hole card thrown away.
/// </summary>
public sealed record CrazyPineappleAction(
	HoldEmPosition Actor,
	CrazyPineappleActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	int? CommunityCount = null,
	string? Card = null)
{
	public static CrazyPineappleAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, CrazyPineappleActionKind.Fold, PotAfter: potAfter);

	public static CrazyPineappleAction Check(HoldEmPosition actor, int? communityCount = null) =>
		new(actor, CrazyPineappleActionKind.Check, CommunityCount: communityCount);

	public static CrazyPineappleAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, CrazyPineappleActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static CrazyPineappleAction Bet(
		HoldEmPosition actor,
		int amount,
		int potAfter,
		int? communityCount = null) =>
		new(actor, CrazyPineappleActionKind.Bet, amount, potAfter, StreetBet: amount, CommunityCount: communityCount);

	public static CrazyPineappleAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, CrazyPineappleActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static CrazyPineappleAction Discard(HoldEmPosition actor, string card) =>
		new(actor, CrazyPineappleActionKind.Discard, CommunityCount: 3, Card: card);
}

/// <summary>
///     A five-handed Crazy Pineapple deal. Hole cards are three-card hands. Nets are the stack
///     change when the hand is over. <see cref="AwardedAmount" /> is the gross pot shown for the winner.
/// </summary>
public sealed record CrazyPineappleHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<string> Board { get; init; }

	public required IReadOnlyList<CrazyPineappleAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }
}

/// <summary>
///     Known deals for the five-player Crazy Pineapple Playwright orbit. Blinds are 5 and 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     The first discard is the first live seat left of the dealer. Each later discard is the
///     lowest seat number that has not discarded yet.
/// </summary>
public static class CrazyPineappleFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static CrazyPineappleHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h 4h", "5h 6h 7h", "Qh Kh Ah", "8h 9h Th", "6c 7c 8c"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
		Actions =
		[
			CrazyPineappleAction.Fold(HoldEmPosition.UnderTheGun),
			CrazyPineappleAction.Fold(HoldEmPosition.Cutoff),
			CrazyPineappleAction.Fold(HoldEmPosition.Button),
			CrazyPineappleAction.Fold(HoldEmPosition.SmallBlind)
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

	public static CrazyPineappleHandScript DiscardThenFlopBet { get; } = new()
	{
		Name = "Discard then flop bet",
		HoleCards = Holes("6h 7h 8h", "9h Th Jh", "2c 3c 4c", "Ah Ad 5h", "6c 7c 8c"),
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			CrazyPineappleAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			CrazyPineappleAction.Fold(HoldEmPosition.Cutoff),
			CrazyPineappleAction.Fold(HoldEmPosition.Button),
			CrazyPineappleAction.Fold(HoldEmPosition.SmallBlind),
			CrazyPineappleAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 65),
			// Dealer is seat 1. Live seats are the big blind (3) and under the gun (4).
			CrazyPineappleAction.Discard(HoldEmPosition.BigBlind, "4c"),
			CrazyPineappleAction.Discard(HoldEmPosition.UnderTheGun, "5h"),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			CrazyPineappleAction.Bet(HoldEmPosition.UnderTheGun, amount: 40, potAfter: 105),
			CrazyPineappleAction.Fold(HoldEmPosition.BigBlind)
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

	public static CrazyPineappleHandScript Checkdown { get; } = new()
	{
		Name = "Checkdown",
		HoleCards = Holes("As Ac 2d", "8c 9c Tc", "Ks Qs 3d", "4h 5h 6h", "4c 5c 6c"),
		Board = ["Ah", "Kd", "7c", "2s", "9h"],
		Actions =
		[
			CrazyPineappleAction.Fold(HoldEmPosition.UnderTheGun),
			CrazyPineappleAction.Fold(HoldEmPosition.Cutoff),
			CrazyPineappleAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			CrazyPineappleAction.Fold(HoldEmPosition.SmallBlind),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 2. Live seats are the button (2) and the big blind (4).
			CrazyPineappleAction.Discard(HoldEmPosition.BigBlind, "3d"),
			CrazyPineappleAction.Discard(HoldEmPosition.Button, "2d"),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			CrazyPineappleAction.Check(HoldEmPosition.Button),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			CrazyPineappleAction.Check(HoldEmPosition.Button),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			CrazyPineappleAction.Check(HoldEmPosition.Button)
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

	public static CrazyPineappleHandScript RiverRaise { get; } = new()
	{
		Name = "River raise",
		HoleCards = Holes("As Ac 4c", "8c 9d Tc", "5h 6h 7h", "2c 3c 4h", "2d 3d 5c"),
		Board = ["Ah", "9c", "4d", "3h", "8s"],
		Actions =
		[
			CrazyPineappleAction.Fold(HoldEmPosition.UnderTheGun),
			CrazyPineappleAction.Fold(HoldEmPosition.Cutoff),
			CrazyPineappleAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			CrazyPineappleAction.Fold(HoldEmPosition.SmallBlind),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 3. Live seats are the big blind (0) and the button (3).
			CrazyPineappleAction.Discard(HoldEmPosition.BigBlind, "7h"),
			CrazyPineappleAction.Discard(HoldEmPosition.Button, "4c"),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			CrazyPineappleAction.Check(HoldEmPosition.Button),
			CrazyPineappleAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 4),
			CrazyPineappleAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65),
			CrazyPineappleAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			CrazyPineappleAction.Bet(HoldEmPosition.Button, amount: 40, potAfter: 105),
			CrazyPineappleAction.Raise(HoldEmPosition.BigBlind, raiseTo: 100, potAfter: 205),
			CrazyPineappleAction.Call(HoldEmPosition.Button, callAmount: 60, potAfter: 265)
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

	/// <summary>
	///     Four hands. The scenario deals hand <c>n</c> from seat <c>n</c>.
	/// </summary>
	public static IReadOnlyList<CrazyPineappleHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		DiscardThenFlopBet,
		Checkdown,
		RiverRaise
	];

	/// <summary>
	///     Discard actors in server order. Empty when the hand ends before the flop.
	///     The first actor is the first non-folded seat left of the dealer. Each later actor
	///     is the lowest remaining seat number.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DiscardOrder(CrazyPineappleHandScript script, int dealerSeat)
	{
		var folded = new HashSet<HoldEmPosition>();
		foreach (var action in script.Actions)
		{
			if (action.Kind == CrazyPineappleActionKind.Discard)
				break;

			if (action.Kind == CrazyPineappleActionKind.Fold)
				folded.Add(action.Actor);
		}

		var live = script.HoleCards.Keys
			.Where(position => !folded.Contains(position))
			.Select(position => (Position: position, Seat: HoldEmSeats.Index(SeatCount, dealerSeat, position)))
			.ToList();
		if (live.Count < 2)
			return [];

		var firstSeat = -1;
		var cursor = (dealerSeat + 1) % SeatCount;
		for (var step = 0; step < SeatCount; step++)
		{
			if (live.Any(player => player.Seat == cursor))
			{
				firstSeat = cursor;
				break;
			}

			cursor = (cursor + 1) % SeatCount;
		}

		var order = live
			.Where(player => player.Seat != firstSeat)
			.OrderBy(player => player.Seat)
			.Select(player => player.Position)
			.ToList();
		order.Insert(0, live.Single(player => player.Seat == firstSeat).Position);
		return order;
	}

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Holes(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff)
	{
		return new Dictionary<HoldEmPosition, IReadOnlyList<string>>
		{
			[HoldEmPosition.Button] = Trio(button),
			[HoldEmPosition.SmallBlind] = Trio(smallBlind),
			[HoldEmPosition.BigBlind] = Trio(bigBlind),
			[HoldEmPosition.UnderTheGun] = Trio(underTheGun),
			[HoldEmPosition.Cutoff] = Trio(cutoff)
		};
	}

	private static IReadOnlyList<string> Trio(string cards) =>
		cards.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
