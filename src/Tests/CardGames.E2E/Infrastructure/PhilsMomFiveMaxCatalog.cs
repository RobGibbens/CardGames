namespace CardGames.E2E.Infrastructure;

public enum PhilsMomActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	Discard
}

/// <summary>
///     One betting or discard action. <see cref="Card" /> is the single hole card thrown away.
///     Phil's Mom requires exactly one discard before the flop and exactly one after it.
/// </summary>
public sealed record PhilsMomAction(
	HoldEmPosition Actor,
	PhilsMomActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	int? CommunityCount = null,
	string? Card = null)
{
	public static PhilsMomAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, PhilsMomActionKind.Fold, PotAfter: potAfter);

	public static PhilsMomAction Check(HoldEmPosition actor, int? communityCount = null) =>
		new(actor, PhilsMomActionKind.Check, CommunityCount: communityCount);

	public static PhilsMomAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, PhilsMomActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static PhilsMomAction Bet(
		HoldEmPosition actor,
		int amount,
		int potAfter,
		int? communityCount = null) =>
		new(actor, PhilsMomActionKind.Bet, amount, potAfter, StreetBet: amount, CommunityCount: communityCount);

	public static PhilsMomAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, PhilsMomActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static PhilsMomAction Discard(HoldEmPosition actor, string card, int communityCount) =>
		new(actor, PhilsMomActionKind.Discard, CommunityCount: communityCount, Card: card);
}

/// <summary>
///     A five-handed Phil's Mom deal. Hole cards are four-card hands. Nets are the stack
///     change when the hand is over. <see cref="AwardedAmount" /> is the gross pot shown for the winner.
/// </summary>
public sealed record PhilsMomHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<string> Board { get; init; }

	public required IReadOnlyList<PhilsMomAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }
}

/// <summary>
///     Known deals for the five-player Phil's Mom Playwright orbit. Blinds are 5 and 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     Each player discards exactly one card before the flop and exactly one after flop betting.
///     The first discard of a round is the first live seat left of the dealer. Each later discard
///     is the lowest seat number that has not discarded yet.
/// </summary>
public static class PhilsMomFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static PhilsMomHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h 4h 5h", "6h 7h 8h 9h", "Jh Qh Kh Ah", "8c 9c Tc Jc", "2c 3c 6c 7c"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
		Actions =
		[
			PhilsMomAction.Fold(HoldEmPosition.UnderTheGun),
			PhilsMomAction.Fold(HoldEmPosition.Cutoff),
			PhilsMomAction.Fold(HoldEmPosition.Button),
			PhilsMomAction.Fold(HoldEmPosition.SmallBlind)
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

	public static PhilsMomHandScript DiscardBeforeFlopThenFlopFold { get; } = new()
	{
		Name = "Discard before the flop, then fold on the flop",
		HoleCards = Holes("6h 7h 8h 9h", "9c Tc Jc Qc", "2c 3c 4c 5c", "Ah Ad 5h 6c", "6d 7d 8d 9d"),
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			PhilsMomAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			PhilsMomAction.Fold(HoldEmPosition.Cutoff),
			PhilsMomAction.Fold(HoldEmPosition.Button),
			PhilsMomAction.Fold(HoldEmPosition.SmallBlind),
			PhilsMomAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 65),
			// Dealer is seat 1. Live seats are the big blind (3) and under the gun (4).
			// The board is still empty. Each player discards exactly one of four.
			PhilsMomAction.Discard(HoldEmPosition.BigBlind, "4c", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.UnderTheGun, "5h", communityCount: 0),
			PhilsMomAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			PhilsMomAction.Bet(HoldEmPosition.UnderTheGun, amount: 40, potAfter: 105),
			PhilsMomAction.Fold(HoldEmPosition.BigBlind)
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

	public static PhilsMomHandScript FiveWayCheckdown { get; } = new()
	{
		Name = "Five-way checkdown",
		// The button keeps As Ks and wins with two pair, aces and kings.
		// The cutoff is dealt Ah Ad and discards both. If those discards are ignored,
		// the cutoff has trip aces and the showdown winner is wrong.
		HoleCards = Holes("As Ks 2d 3d", "8h 9h Th Jh", "Qs Js 6h 7d", "4c 5c 6c 8c", "Ah Ad 4h 5h"),
		Board = ["Ac", "Kd", "7c", "2s", "9c"],
		Actions =
		[
			PhilsMomAction.Call(HoldEmPosition.UnderTheGun, callAmount: 10, potAfter: 25, streetBet: 10),
			PhilsMomAction.Call(HoldEmPosition.Cutoff, callAmount: 10, potAfter: 35, streetBet: 10),
			PhilsMomAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 45, streetBet: 10),
			PhilsMomAction.Call(HoldEmPosition.SmallBlind, callAmount: 5, potAfter: 50, streetBet: 10),
			PhilsMomAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 2. The small blind acts first. Later discards are the lowest
			// remaining seat numbers: under the gun (0), cutoff (1), button (2), big blind (4).
			PhilsMomAction.Discard(HoldEmPosition.SmallBlind, "8h", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.UnderTheGun, "4c", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.Cutoff, "Ah", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.Button, "2d", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.BigBlind, "6h", communityCount: 0),
			PhilsMomAction.Check(HoldEmPosition.SmallBlind, communityCount: 3),
			PhilsMomAction.Check(HoldEmPosition.BigBlind),
			PhilsMomAction.Check(HoldEmPosition.UnderTheGun),
			PhilsMomAction.Check(HoldEmPosition.Cutoff),
			PhilsMomAction.Check(HoldEmPosition.Button),
			PhilsMomAction.Discard(HoldEmPosition.SmallBlind, "9h", communityCount: 3),
			PhilsMomAction.Discard(HoldEmPosition.UnderTheGun, "5c", communityCount: 3),
			PhilsMomAction.Discard(HoldEmPosition.Cutoff, "Ad", communityCount: 3),
			PhilsMomAction.Discard(HoldEmPosition.Button, "3d", communityCount: 3),
			PhilsMomAction.Discard(HoldEmPosition.BigBlind, "7d", communityCount: 3),
			PhilsMomAction.Check(HoldEmPosition.SmallBlind, communityCount: 4),
			PhilsMomAction.Check(HoldEmPosition.BigBlind),
			PhilsMomAction.Check(HoldEmPosition.UnderTheGun),
			PhilsMomAction.Check(HoldEmPosition.Cutoff),
			PhilsMomAction.Check(HoldEmPosition.Button),
			PhilsMomAction.Check(HoldEmPosition.SmallBlind, communityCount: 5),
			PhilsMomAction.Check(HoldEmPosition.BigBlind),
			PhilsMomAction.Check(HoldEmPosition.UnderTheGun),
			PhilsMomAction.Check(HoldEmPosition.Cutoff),
			PhilsMomAction.Check(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 40,
			[HoldEmPosition.SmallBlind] = -10,
			[HoldEmPosition.BigBlind] = -10,
			[HoldEmPosition.UnderTheGun] = -10,
			[HoldEmPosition.Cutoff] = -10
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 50,
		WonByFold = false
	};

	public static PhilsMomHandScript RiverRaise { get; } = new()
	{
		Name = "River raise",
		// The button keeps As Ks. The big blind is dealt Ah Ad and discards both.
		// Kept cards make the button's two pair best. Ignoring the discards gives the
		// big blind trip aces and a different winner.
		HoleCards = Holes("As Ks 2d 3d", "9d Td Jd Qd", "Ah Ad 7h 8h", "4c 5c 6c 8c", "4h 5h 6h 9h"),
		Board = ["Ac", "Kd", "4d", "3h", "9c"],
		Actions =
		[
			PhilsMomAction.Fold(HoldEmPosition.UnderTheGun),
			PhilsMomAction.Fold(HoldEmPosition.Cutoff),
			PhilsMomAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			PhilsMomAction.Fold(HoldEmPosition.SmallBlind),
			PhilsMomAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 3. Live seats are the big blind (0) and the button (3).
			PhilsMomAction.Discard(HoldEmPosition.BigBlind, "Ah", communityCount: 0),
			PhilsMomAction.Discard(HoldEmPosition.Button, "2d", communityCount: 0),
			PhilsMomAction.Check(HoldEmPosition.BigBlind, communityCount: 3),
			PhilsMomAction.Check(HoldEmPosition.Button),
			PhilsMomAction.Discard(HoldEmPosition.BigBlind, "Ad", communityCount: 3),
			PhilsMomAction.Discard(HoldEmPosition.Button, "3d", communityCount: 3),
			PhilsMomAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 4),
			PhilsMomAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65),
			PhilsMomAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			PhilsMomAction.Bet(HoldEmPosition.Button, amount: 40, potAfter: 105),
			PhilsMomAction.Raise(HoldEmPosition.BigBlind, raiseTo: 100, potAfter: 205),
			PhilsMomAction.Call(HoldEmPosition.Button, callAmount: 60, potAfter: 265)
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
	public static IReadOnlyList<PhilsMomHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		DiscardBeforeFlopThenFlopFold,
		FiveWayCheckdown,
		RiverRaise
	];

	/// <summary>
	///     Discard actors for a one-based discard round, in server order.
	///     Empty when the hand ends before that round.
	///     The first actor is the first non-folded seat left of the dealer. Each later actor
	///     is the lowest remaining seat number. Folds on an earlier street are excluded.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DiscardOrder(PhilsMomHandScript script, int dealerSeat, int round)
	{
		var folded = new HashSet<HoldEmPosition>();
		var seenRounds = 0;
		var inDiscard = false;
		foreach (var action in script.Actions)
		{
			if (action.Kind == PhilsMomActionKind.Discard)
			{
				if (!inDiscard)
				{
					seenRounds++;
					inDiscard = true;
					if (seenRounds == round)
						break;
				}

				continue;
			}

			inDiscard = false;
			if (action.Kind == PhilsMomActionKind.Fold)
				folded.Add(action.Actor);
		}

		if (seenRounds < round)
			return [];

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
			[HoldEmPosition.Button] = Quartet(button),
			[HoldEmPosition.SmallBlind] = Quartet(smallBlind),
			[HoldEmPosition.BigBlind] = Quartet(bigBlind),
			[HoldEmPosition.UnderTheGun] = Quartet(underTheGun),
			[HoldEmPosition.Cutoff] = Quartet(cutoff)
		};
	}

	private static IReadOnlyList<string> Quartet(string cards) =>
		cards.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
