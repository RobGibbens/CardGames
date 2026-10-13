namespace CardGames.E2E.Infrastructure;

/// <summary>
///     How third street is dealt. The host's start button uses seat order.
///     Continuous play uses the flow handler, which starts left of the dealer.
/// </summary>
public enum BaseballDealPath
{
	SeatOrder,
	LeftOfDealer
}

public enum BaseballStreet
{
	Third,
	Fourth,
	Fifth,
	Sixth,
	Seventh
}

public enum BaseballActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	Buy,
	Decline
}

/// <summary>
///     One Baseball action. Bet and raise amounts are the street total the action panel submits.
/// </summary>
public sealed record BaseballAction(
	HoldEmPosition Actor,
	BaseballActionKind Kind,
	BaseballStreet Street,
	int? Amount = null,
	int? PotBefore = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? UpCards = null,
	IReadOnlyList<string>? OwnCards = null)
{
	public static BaseballAction Fold(
		HoldEmPosition actor,
		BaseballStreet street,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(actor, BaseballActionKind.Fold, street, UpCards: upCards);

	public static BaseballAction Check(HoldEmPosition actor, BaseballStreet street) =>
		new(actor, BaseballActionKind.Check, street);

	public static BaseballAction Call(
		HoldEmPosition actor,
		BaseballStreet street,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, BaseballActionKind.Call, street, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static BaseballAction Bet(
		HoldEmPosition actor,
		BaseballStreet street,
		int amount,
		int potAfter,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? upCards = null) =>
		new(
			actor,
			BaseballActionKind.Bet,
			street,
			amount,
			PotAfter: potAfter,
			StreetBet: amount,
			UpCards: upCards);

	public static BaseballAction Raise(
		HoldEmPosition actor,
		BaseballStreet street,
		int raiseTo,
		int potAfter) =>
		new(actor, BaseballActionKind.RaiseTo, street, raiseTo, PotAfter: potAfter, StreetBet: raiseTo);

	public static BaseballAction Buy(
		HoldEmPosition actor,
		BaseballStreet street,
		int potBefore,
		int potAfter,
		IReadOnlyList<string> ownCards) =>
		new(actor, BaseballActionKind.Buy, street, PotBefore: potBefore, PotAfter: potAfter, OwnCards: ownCards);

	public static BaseballAction Decline(
		HoldEmPosition actor,
		BaseballStreet street,
		int potBefore,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> upCards,
		IReadOnlyList<string> ownCards) =>
		new(
			actor,
			BaseballActionKind.Decline,
			street,
			PotBefore: potBefore,
			UpCards: upCards,
			OwnCards: ownCards);
}

/// <summary>
///     Cards a seat receives, in deal order. <see cref="BuyCard" /> is the extra face-down card
///     taken when that seat accepts its face-up four.
/// </summary>
public sealed record BaseballHolding
{
	public required string FirstHole { get; init; }

	public required string SecondHole { get; init; }

	public required string Door { get; init; }

	public string? Fourth { get; init; }

	public string? Fifth { get; init; }

	public string? Sixth { get; init; }

	public string? Seventh { get; init; }

	public string? BuyCard { get; init; }
}

/// <summary>
///     A five-handed Baseball deal and the line played through the UI.
///     Nets are the stack change when the hand is over. Positions are offsets from the dealer.
/// </summary>
public sealed record BaseballHandScript
{
	public required string Name { get; init; }

	public required BaseballDealPath ThirdStreetPath { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, BaseballHolding> Holdings { get; init; }

	public required IReadOnlyList<BaseballAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition BringIn { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public int OpeningPot { get; init; }

	public bool WonByFold { get; init; }

	public bool ShowsWild { get; init; }
}

public sealed class BaseballLedger
{
	private readonly int[] _stacks;

	public BaseballLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(BaseballHandScript script, int dealerSeat)
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
///     Known deals for the five-player Baseball orbit.
///     Ante 5, bring-in 10, small bet 10, big bet 20, buy price 10.
///     The bring-in is the big blind's deuce in every hand.
/// </summary>
public static class BaseballFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 5;
	public const int BringInAmount = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int BuyPrice = 10;
	public const int OpeningPot = 35;

	public static BaseballHandScript FoldToBringIn { get; } = new()
	{
		Name = "Fold to the bring-in",
		ThirdStreetPath = BaseballDealPath.SeatOrder,
		Holdings = Hands(
			Holes("5d 6d", "Kh"),
			Holes("5h 6h", "Qh"),
			Holes("5c 6c", "2c"),
			Holes("5s 6s", "Jh"),
			Holes("7d 8d", "Th")),
		Actions =
		[
			BaseballAction.Fold(HoldEmPosition.UnderTheGun, BaseballStreet.Third, Doors(
				"Kh", "Qh", "2c", "Jh", "Th")),
			BaseballAction.Fold(HoldEmPosition.Cutoff, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.Button, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.SmallBlind, BaseballStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static BaseballHandScript DeclineFour { get; } = new()
	{
		Name = "Decline a four",
		ThirdStreetPath = BaseballDealPath.LeftOfDealer,
		Holdings = Hands(
			Holes("7h 8h", "Qc"),
			Holes("7c 8c", "4h"),
			Holes("7s 8s", "2c"),
			Holes("9d Td", "Jd"),
			Holes("9c Tc", "Jc")),
		Actions =
		[
			BaseballAction.Decline(
				HoldEmPosition.SmallBlind,
				BaseballStreet.Third,
				potBefore: Ante * SeatCount,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.SmallBlind] = ["4h"]
				},
				ownCards: ["7c", "8c", "4h"]),
			BaseballAction.Fold(HoldEmPosition.UnderTheGun, BaseballStreet.Third, Doors(
				"Qc", "4h", "2c", "Jd", "Jc")),
			BaseballAction.Fold(HoldEmPosition.Cutoff, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.Button, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.SmallBlind, BaseballStreet.Third)
		],
		Nets = LoseAntes(winner: HoldEmPosition.BigBlind, winnerNet: 20),
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = OpeningPot,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static BaseballHandScript BuyFourth { get; } = new()
	{
		Name = "Buy a four",
		ThirdStreetPath = BaseballDealPath.LeftOfDealer,
		Holdings = Hands(
			Holes("6d 7d", "Qd"),
			Holes("6h 7h", "Qh"),
			Holes("6c 7c", "2c", fourth: "Kh", fifth: "Kd"),
			Holes("6s 7s", "Ah", fourth: "4d", buy: "8s"),
			Holes("8d 9c", "Jh")),
		Actions =
		[
			BaseballAction.Call(
				HoldEmPosition.UnderTheGun,
				BaseballStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount),
			BaseballAction.Fold(HoldEmPosition.Cutoff, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.Button, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.SmallBlind, BaseballStreet.Third),
			BaseballAction.Buy(
				HoldEmPosition.UnderTheGun,
				BaseballStreet.Fourth,
				potBefore: 45,
				potAfter: 55,
				ownCards: ["6s", "7s", "Ah", "4d", "8s"]),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Fourth),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Fourth),
			BaseballAction.Bet(
				HoldEmPosition.BigBlind,
				BaseballStreet.Fifth,
				amount: BigBet,
				potAfter: 75,
				upCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.UnderTheGun] = ["Ah", "4d"],
					[HoldEmPosition.BigBlind] = ["2c", "Kh", "Kd"]
				}),
			BaseballAction.Fold(HoldEmPosition.UnderTheGun, BaseballStreet.Fifth)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 40,
			[HoldEmPosition.UnderTheGun] = -25,
			[HoldEmPosition.Button] = -Ante,
			[HoldEmPosition.SmallBlind] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		BringIn = HoldEmPosition.BigBlind,
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 75,
		OpeningPot = OpeningPot,
		WonByFold = true
	};

	public static BaseballHandScript WildShowdown { get; } = new()
	{
		Name = "Wild-card showdown",
		ThirdStreetPath = BaseballDealPath.LeftOfDealer,
		Holdings = Hands(
			Holes("5d 6d", "Jd"),
			Holes("5h 6h", "Qh"),
			Holes("Kc Kd", "2c", fourth: "Ah", fifth: "Ad", sixth: "7h", seventh: "9h"),
			Holes("Qs Qd", "8d", fourth: "5c", fifth: "6s", sixth: "6c", seventh: "4s"),
			Holes("8h 9d", "Tc")),
		Actions =
		[
			BaseballAction.Call(
				HoldEmPosition.UnderTheGun,
				BaseballStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount),
			BaseballAction.Fold(HoldEmPosition.Cutoff, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.Button, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.SmallBlind, BaseballStreet.Third),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Fourth),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Fourth),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Fifth),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Fifth),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Sixth),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Sixth),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Seventh),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Seventh)
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

	public static BaseballHandScript BigBetRaise { get; } = new()
	{
		Name = "Big-bet raise",
		ThirdStreetPath = BaseballDealPath.LeftOfDealer,
		Holdings = Hands(
			Holes("5s 6s", "Kd"),
			Holes("5d 9s", "Qd"),
			Holes("7c 8c", "2c", fourth: "Ah", fifth: "Ad"),
			Holes("7d 8h", "8d", fourth: "5h", fifth: "6h"),
			Holes("4c 7h", "Jc")),
		Actions =
		[
			BaseballAction.Call(
				HoldEmPosition.UnderTheGun,
				BaseballStreet.Third,
				callAmount: BringInAmount,
				potAfter: 45,
				streetBet: BringInAmount),
			BaseballAction.Fold(HoldEmPosition.Cutoff, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.Button, BaseballStreet.Third),
			BaseballAction.Fold(HoldEmPosition.SmallBlind, BaseballStreet.Third),
			BaseballAction.Check(HoldEmPosition.BigBlind, BaseballStreet.Fourth),
			BaseballAction.Check(HoldEmPosition.UnderTheGun, BaseballStreet.Fourth),
			BaseballAction.Bet(
				HoldEmPosition.BigBlind,
				BaseballStreet.Fifth,
				amount: BigBet,
				potAfter: 65),
			BaseballAction.Raise(
				HoldEmPosition.UnderTheGun,
				BaseballStreet.Fifth,
				raiseTo: BigBet * 2,
				potAfter: 105),
			BaseballAction.Fold(HoldEmPosition.BigBlind, BaseballStreet.Fifth)
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

	public static IReadOnlyList<BaseballHandScript> Orbit { get; } =
	[
		FoldToBringIn,
		DeclineFour,
		BuyFourth,
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

	private static Dictionary<HoldEmPosition, BaseballHolding> Hands(
		BaseballHolding button,
		BaseballHolding smallBlind,
		BaseballHolding bigBlind,
		BaseballHolding underTheGun,
		BaseballHolding cutoff) =>
		new()
		{
			[HoldEmPosition.Button] = button,
			[HoldEmPosition.SmallBlind] = smallBlind,
			[HoldEmPosition.BigBlind] = bigBlind,
			[HoldEmPosition.UnderTheGun] = underTheGun,
			[HoldEmPosition.Cutoff] = cutoff
		};

	private static BaseballHolding Holes(
		string hole,
		string door,
		string? fourth = null,
		string? fifth = null,
		string? sixth = null,
		string? seventh = null,
		string? buy = null)
	{
		var cards = hole.Split(' ');
		return new BaseballHolding
		{
			FirstHole = cards[0],
			SecondHole = cards[1],
			Door = door,
			Fourth = fourth,
			Fifth = fifth,
			Sixth = sixth,
			Seventh = seventh,
			BuyCard = buy
		};
	}
}
