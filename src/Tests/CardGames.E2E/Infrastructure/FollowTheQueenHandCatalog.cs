namespace CardGames.E2E.Infrastructure;

public enum FollowTheQueenStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum FollowTheQueenActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

/// <summary>
///     One Follow the Queen action. Bet and raise amounts are the street total the action panel submits.
///     <see cref="WildLabels" /> are the ranks the wild-card panel should show before the action,
///     such as "Queens" and "Kings".
/// </summary>
public sealed record FollowTheQueenAction(
	HoldEmPosition Actor,
	FollowTheQueenActionKind Kind,
	FollowTheQueenStreet Street,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null,
	IReadOnlyList<string>? WildLabels = null)
{
	public static FollowTheQueenAction Fold(
		HoldEmPosition actor,
		FollowTheQueenStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(actor, FollowTheQueenActionKind.Fold, street, UpCards: upCards, WildLabels: wildLabels);

	public static FollowTheQueenAction Check(HoldEmPosition actor, FollowTheQueenStreet street) =>
		new(actor, FollowTheQueenActionKind.Check, street);

	public static FollowTheQueenAction Call(
		HoldEmPosition actor,
		FollowTheQueenStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(
			actor,
			FollowTheQueenActionKind.Call,
			street,
			PotAfter: potAfter,
			CallAmount: callAmount,
			StreetBet: streetBet,
			WildLabels: wildLabels);

	public static FollowTheQueenAction Bet(
		HoldEmPosition actor,
		FollowTheQueenStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null,
		IReadOnlyList<string>? wildLabels = null) =>
		new(
			actor,
			FollowTheQueenActionKind.Bet,
			street,
			amount,
			PotAfter: potAfter,
			StreetBet: amount,
			UpCards: upCards,
			WildLabels: wildLabels);

	public static FollowTheQueenAction Raise(
		HoldEmPosition actor,
		FollowTheQueenStreet street,
		int raiseTo,
		int potAfter) =>
		new(actor, FollowTheQueenActionKind.RaiseTo, street, raiseTo, PotAfter: potAfter, StreetBet: raiseTo);
}

/// <summary>
///     Cards a seat receives, in deal order.
/// </summary>
public sealed record FollowTheQueenHolding
{
	public required string FirstHole { get; init; }

	public required string SecondHole { get; init; }

	public required string Door { get; init; }

	public string? Fourth { get; init; }

	public string? Fifth { get; init; }

	public string? Sixth { get; init; }

	public string? Seventh { get; init; }
}

/// <summary>
///     A five-handed Follow the Queen deal and the line played through the UI.
///     Nets are the stack change when the hand is over. Positions are offsets from the dealer.
/// </summary>
public sealed record FollowTheQueenHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, FollowTheQueenHolding> Holdings { get; init; }

	public required IReadOnlyList<FollowTheQueenAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }

	public bool ShowsWild { get; init; }
}

public sealed class FollowTheQueenLedger
{
	private readonly int[] _stacks;

	public FollowTheQueenLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(FollowTheQueenHandScript script, int dealerSeat)
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
///     Known deals for the five-player Follow the Queen orbit.
///     Ante 5, bring-in 10, small bet 10, big bet 20.
///     Queens are always wild. The face-up card after the last face-up Queen makes that rank wild.
///     The bring-in is the big blind's deuce in every hand.
/// </summary>
public static class FollowTheQueenFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = 35;

	public const string Queens = "Queens";
	public const string Kings = "Kings";
	public const string Deuces = "Deuces";

	public static FollowTheQueenHandScript FoldToBringIn { get; } = new()
	{
		Name = "Fold to the bring-in",
		Holdings = Hands(
			Holes("5d 6d", "Kh"),
			Holes("5h 6h", "9h"),
			Holes("5c 6c", "2c"),
			Holes("5s 6s", "Jh"),
			Holes("7d 8d", "Th")),
		Actions =
		[
			FollowTheQueenAction.Fold(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Third, Doors(
				"Kh", "9h", "2c", "Jh", "Th"), [Queens]),
			FollowTheQueenAction.Fold(HoldEmPosition.Cutoff, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.Button, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.SmallBlind, FollowTheQueenStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The button's queen is the last third-street up-card, so only queens are wild until fourth street.
	///     The big blind's king is the next face-up card and becomes the follow rank.
	/// </summary>
	public static FollowTheQueenHandScript DoorQueen { get; } = new()
	{
		Name = "Door queen sets the follow rank",
		Holdings = Hands(
			Holes("5s 6s", "Qh"),
			Holes("5d 6d", "Kh"),
			Holes("7c 8c", "2c", fourth: "Kd"),
			Holes("7d 8h", "Jh", fourth: "4s"),
			Holes("4c 3h", "Th")),
		Actions =
		[
			FollowTheQueenAction.Call(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Queens]),
			FollowTheQueenAction.Fold(HoldEmPosition.Cutoff, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.Button, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.SmallBlind, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Bet(
				HoldEmPosition.BigBlind,
				FollowTheQueenStreet.Fourth,
				amount: SmallBet,
				potAfter: 55,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["2c", "Kd"],
					[HoldEmPosition.UnderTheGun] = ["Jh", "4s"]
				},
				wildLabels: [Queens, Kings]),
			FollowTheQueenAction.Fold(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Fourth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 30,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 55,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The small blind's queen makes deuces wild. The big blind's later queen replaces that with kings.
	///     The under-the-gun king is wild, so that seat's pair of jacks acts first.
	/// </summary>
	public static FollowTheQueenHandScript QueenReplaced { get; } = new()
	{
		Name = "A later queen replaces the follow rank",
		Holdings = Hands(
			Holes("8s 9s", "8h"),
			Holes("5h 6h", "Qh"),
			Holes("7c 8c", "2c", fourth: "Qd"),
			Holes("7d 9d", "Jh", fourth: "Kh"),
			Holes("4c 3h", "Th")),
		Actions =
		[
			FollowTheQueenAction.Call(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Queens, Deuces]),
			FollowTheQueenAction.Fold(HoldEmPosition.Cutoff, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.Button, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.SmallBlind, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Bet(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Fourth,
				amount: SmallBet,
				potAfter: 55,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.BigBlind] = ["2c", "Qd"],
					[HoldEmPosition.UnderTheGun] = ["Jh", "Kh"]
				},
				wildLabels: [Queens, Kings]),
			FollowTheQueenAction.Fold(HoldEmPosition.BigBlind, FollowTheQueenStreet.Fourth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 30,
			[HoldEmPosition.BigBlind] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 55,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	/// <summary>
	///     The small blind's queen makes deuces wild. The big blind's deuce and two aces
	///     beat the under-the-gun two pair at showdown.
	/// </summary>
	public static FollowTheQueenHandScript WildShowdown { get; } = new()
	{
		Name = "Wild-card showdown",
		Holdings = Hands(
			Holes("5d 6d", "Jd"),
			Holes("5h 6h", "Qh"),
			Holes("Kc Kd", "2c", fourth: "Ah", fifth: "Ad", sixth: "7h", seventh: "9h"),
			Holes("4s 3d", "8d", fourth: "5c", fifth: "6s", sixth: "6c", seventh: "4d"),
			Holes("8h 9d", "Tc")),
		Actions =
		[
			FollowTheQueenAction.Call(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Queens, Deuces]),
			FollowTheQueenAction.Fold(HoldEmPosition.Cutoff, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.Button, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.SmallBlind, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Check(HoldEmPosition.BigBlind, FollowTheQueenStreet.Fourth),
			FollowTheQueenAction.Check(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Fourth),
			FollowTheQueenAction.Check(HoldEmPosition.BigBlind, FollowTheQueenStreet.Fifth),
			FollowTheQueenAction.Check(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Fifth),
			FollowTheQueenAction.Check(HoldEmPosition.BigBlind, FollowTheQueenStreet.Sixth),
			FollowTheQueenAction.Check(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Sixth),
			FollowTheQueenAction.Check(HoldEmPosition.BigBlind, FollowTheQueenStreet.Seventh),
			FollowTheQueenAction.Check(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Seventh)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 30,
			[HoldEmPosition.UnderTheGun] = -15,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 45,
		OpeningPot = OpeningPot,
		WonByFold = false,
		ShowsWild = true
	};

	public static FollowTheQueenHandScript BigBetRaise { get; } = new()
	{
		Name = "Big-bet raise",
		Holdings = Hands(
			Holes("5s 6s", "Kd"),
			Holes("5d 4h", "9s"),
			Holes("7c 8c", "2c", fourth: "Ah", fifth: "Ad"),
			Holes("7d 8h", "8d", fourth: "5h", fifth: "6h"),
			Holes("4c 3h", "Jc")),
		Actions =
		[
			FollowTheQueenAction.Call(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount,
				wildLabels: [Queens]),
			FollowTheQueenAction.Fold(HoldEmPosition.Cutoff, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.Button, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Fold(HoldEmPosition.SmallBlind, FollowTheQueenStreet.Third),
			FollowTheQueenAction.Check(HoldEmPosition.BigBlind, FollowTheQueenStreet.Fourth),
			FollowTheQueenAction.Check(HoldEmPosition.UnderTheGun, FollowTheQueenStreet.Fourth),
			FollowTheQueenAction.Bet(
				HoldEmPosition.BigBlind,
				FollowTheQueenStreet.Fifth,
				amount: BigBet,
				potAfter: 65),
			FollowTheQueenAction.Raise(
				HoldEmPosition.UnderTheGun,
				FollowTheQueenStreet.Fifth,
				raiseTo: BigBet * 2,
				potAfter: 105),
			FollowTheQueenAction.Fold(HoldEmPosition.BigBlind, FollowTheQueenStreet.Fifth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.UnderTheGun] = 50,
			[HoldEmPosition.BigBlind] = -35,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.UnderTheGun,
		AwardedAmount = 105,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static IReadOnlyList<FollowTheQueenHandScript> Orbit { get; } =
	[
		FoldToBringIn,
		DoorQueen,
		QueenReplaced,
		WildShowdown,
		BigBetRaise
	];

	private static Dictionary<HoldEmPosition, int> LoseAntes(HoldEmPosition winner, int winnerNet)
	{
		var nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		};
		nets[winner] = winnerNet;
		return nets;
	}

	private static Dictionary<HoldEmPosition, IReadOnlyList<string>> Doors(
		string button,
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = [button],
			[HoldEmPosition.SmallBlind] = [smallBlind],
			[HoldEmPosition.BigBlind] = [bigBlind],
			[HoldEmPosition.UnderTheGun] = [underTheGun],
			[HoldEmPosition.Cutoff] = [cutoff]
		};

	private static Dictionary<HoldEmPosition, FollowTheQueenHolding> Hands(
		FollowTheQueenHolding button,
		FollowTheQueenHolding smallBlind,
		FollowTheQueenHolding bigBlind,
		FollowTheQueenHolding underTheGun,
		FollowTheQueenHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static FollowTheQueenHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null)
	{
		var cards = hole.Split(' ');
		return new FollowTheQueenHolding
		{
			FirstHole = cards[0],
			SecondHole = cards[1],
			Door = door,
			Fourth = fourth,
			Fifth = fifth,
			Sixth = sixth,
			Seventh = seventh
		};
	}
}
