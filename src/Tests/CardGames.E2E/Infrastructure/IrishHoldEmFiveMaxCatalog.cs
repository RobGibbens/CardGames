namespace CardGames.E2E.Infrastructure;

public enum IrishHoldEmActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	Discard
}

/// <summary>
///     One betting or discard action. <see cref="Card" /> and <see cref="SecondCard" /> are the
///     two hole cards thrown away. Irish Hold 'Em requires both.
/// </summary>
public sealed record IrishHoldEmAction(
	HoldEmPosition Actor,
	IrishHoldEmActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	int? CommunityCount = null,
	string? Card = null,
	string? SecondCard = null)
{
	public static IrishHoldEmAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, IrishHoldEmActionKind.Fold, PotAfter: potAfter);

	public static IrishHoldEmAction Check(HoldEmPosition actor, int? communityCount = null) =>
		new(actor, IrishHoldEmActionKind.Check, CommunityCount: communityCount);

	public static IrishHoldEmAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, IrishHoldEmActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static IrishHoldEmAction Bet(
		HoldEmPosition actor,
		int amount,
		int potAfter,
		int? communityCount = null) =>
		new(actor, IrishHoldEmActionKind.Bet, amount, potAfter, StreetBet: amount, CommunityCount: communityCount);

	public static IrishHoldEmAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, IrishHoldEmActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static IrishHoldEmAction Discard(HoldEmPosition actor, string firstCard, string secondCard) =>
		new(actor, IrishHoldEmActionKind.Discard, CommunityCount: 3, Card: firstCard, SecondCard: secondCard);
}

/// <summary>
///     A five-handed Irish Hold 'Em deal. Hole cards are four-card hands. Nets are the stack
///     change when the hand is over. <see cref="AwardedAmount" /> is the gross pot shown for the winner.
/// </summary>
public sealed record IrishHoldEmHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<string> Board { get; init; }

	public required IReadOnlyList<IrishHoldEmAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }
}

/// <summary>
///     Known deals for the five-player Irish Hold 'Em Playwright orbit. Blinds are 5 and 10.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     There is no flop betting. The first discard is the first live seat left of the dealer.
///     Each later discard is the lowest seat number that has not discarded yet.
/// </summary>
public static class IrishHoldEmFiveMaxCatalog
{
	public const int SeatCount = 5;

	public static IrishHoldEmHandScript FoldToBigBlind { get; } = new()
	{
		Name = "Fold to the big blind",
		HoleCards = Holes("2h 3h 4h 5h", "6h 7h 8h 9h", "Jh Qh Kh Ah", "8c 9c Tc Jc", "2c 3c 6c 7c"),
		Board = ["2d", "3d", "4d", "5d", "6d"],
		Actions =
		[
			IrishHoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			IrishHoldEmAction.Fold(HoldEmPosition.Cutoff),
			IrishHoldEmAction.Fold(HoldEmPosition.Button),
			IrishHoldEmAction.Fold(HoldEmPosition.SmallBlind)
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

	public static IrishHoldEmHandScript DiscardThenTurnBet { get; } = new()
	{
		Name = "Discard then turn bet",
		HoleCards = Holes("6h 7h 8h 9h", "9c Tc Jc Qc", "2c 3c 4c 5c", "Ah Ad 5h 6c", "6d 7d 8d 9d"),
		Board = ["2s", "3s", "4s", "5s", "6s"],
		Actions =
		[
			IrishHoldEmAction.Raise(HoldEmPosition.UnderTheGun, raiseTo: 30, potAfter: 45),
			IrishHoldEmAction.Fold(HoldEmPosition.Cutoff),
			IrishHoldEmAction.Fold(HoldEmPosition.Button),
			IrishHoldEmAction.Fold(HoldEmPosition.SmallBlind),
			IrishHoldEmAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 65),
			// Dealer is seat 1. Live seats are the big blind (3) and under the gun (4).
			IrishHoldEmAction.Discard(HoldEmPosition.BigBlind, "4c", "5c"),
			IrishHoldEmAction.Discard(HoldEmPosition.UnderTheGun, "5h", "6c"),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			IrishHoldEmAction.Bet(HoldEmPosition.UnderTheGun, amount: 40, potAfter: 105),
			IrishHoldEmAction.Fold(HoldEmPosition.BigBlind)
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

	public static IrishHoldEmHandScript Checkdown { get; } = new()
	{
		Name = "Checkdown",
		HoleCards = Holes("As Ac 2d 3d", "8h 9c Tc Jc", "Ks Qs 3h 4h", "4c 5c 6c 8c", "5d 6d 7d 8d"),
		Board = ["Ah", "Kd", "7c", "2s", "9h"],
		Actions =
		[
			IrishHoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			IrishHoldEmAction.Fold(HoldEmPosition.Cutoff),
			IrishHoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			IrishHoldEmAction.Fold(HoldEmPosition.SmallBlind),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 2. Live seats are the button (2) and the big blind (4).
			IrishHoldEmAction.Discard(HoldEmPosition.BigBlind, "3h", "4h"),
			IrishHoldEmAction.Discard(HoldEmPosition.Button, "2d", "3d"),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 4),
			IrishHoldEmAction.Check(HoldEmPosition.Button),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			IrishHoldEmAction.Check(HoldEmPosition.Button)
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

	public static IrishHoldEmHandScript RiverRaise { get; } = new()
	{
		Name = "River raise",
		HoleCards = Holes("As Ac 4c 9d", "8c Td Qc Kc", "5h 6h 7h 8h", "2c 3c 4h Jc", "2d 3d 5c 6c"),
		Board = ["Ah", "9c", "4d", "3h", "8s"],
		Actions =
		[
			IrishHoldEmAction.Fold(HoldEmPosition.UnderTheGun),
			IrishHoldEmAction.Fold(HoldEmPosition.Cutoff),
			IrishHoldEmAction.Call(HoldEmPosition.Button, callAmount: 10, potAfter: 25, streetBet: 10),
			IrishHoldEmAction.Fold(HoldEmPosition.SmallBlind),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind),
			// Dealer is seat 3. Live seats are the big blind (0) and the button (3).
			IrishHoldEmAction.Discard(HoldEmPosition.BigBlind, "7h", "8h"),
			IrishHoldEmAction.Discard(HoldEmPosition.Button, "4c", "9d"),
			IrishHoldEmAction.Bet(HoldEmPosition.BigBlind, amount: 20, potAfter: 45, communityCount: 4),
			IrishHoldEmAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 65),
			IrishHoldEmAction.Check(HoldEmPosition.BigBlind, communityCount: 5),
			IrishHoldEmAction.Bet(HoldEmPosition.Button, amount: 40, potAfter: 105),
			IrishHoldEmAction.Raise(HoldEmPosition.BigBlind, raiseTo: 100, potAfter: 205),
			IrishHoldEmAction.Call(HoldEmPosition.Button, callAmount: 60, potAfter: 265)
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
	public static IReadOnlyList<IrishHoldEmHandScript> Orbit { get; } =
	[
		FoldToBigBlind,
		DiscardThenTurnBet,
		Checkdown,
		RiverRaise
	];

	/// <summary>
	///     Discard actors in server order. Empty when the hand ends before the flop.
	///     The first actor is the first non-folded seat left of the dealer. Each later actor
	///     is the lowest remaining seat number.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DiscardOrder(IrishHoldEmHandScript script, int dealerSeat)
	{
		var folded = new HashSet<HoldEmPosition>();
		foreach (var action in script.Actions)
		{
			if (action.Kind == IrishHoldEmActionKind.Discard)
				break;

			if (action.Kind == IrishHoldEmActionKind.Fold)
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
