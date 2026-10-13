namespace CardGames.E2E.Infrastructure;

public enum FiveCardDrawActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo,
	StandPat,
	Discard
}

/// <summary>
///     One betting or draw action. <see cref="Discards" /> and <see cref="Draws" /> are card faces.
///     A bet or raise amount is the street total the action panel submits.
/// </summary>
public sealed record FiveCardDrawAction(
	HoldEmPosition Actor,
	FiveCardDrawActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyList<string>? Discards = null,
	IReadOnlyList<string>? Draws = null)
{
	public static FiveCardDrawAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, FiveCardDrawActionKind.Fold, PotAfter: potAfter);

	public static FiveCardDrawAction Check(HoldEmPosition actor) =>
		new(actor, FiveCardDrawActionKind.Check);

	public static FiveCardDrawAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, FiveCardDrawActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static FiveCardDrawAction Bet(HoldEmPosition actor, int amount, int potAfter) =>
		new(actor, FiveCardDrawActionKind.Bet, amount, potAfter, StreetBet: amount);

	public static FiveCardDrawAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, FiveCardDrawActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static FiveCardDrawAction StandPat(HoldEmPosition actor) =>
		new(actor, FiveCardDrawActionKind.StandPat);

	public static FiveCardDrawAction Discard(
		HoldEmPosition actor,
		IReadOnlyList<string> discards,
		IReadOnlyList<string> draws) =>
		new(actor, FiveCardDrawActionKind.Discard, Discards: discards, Draws: draws);
}

/// <summary>
///     A five-handed Five Card Draw deal. Nets are the stack change when the hand is over.
///     <see cref="AwardedAmount" /> is the gross pot shown for the winner.
/// </summary>
public sealed record FiveCardDrawHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<FiveCardDrawAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }
}

/// <summary>
///     Known deals for the five-player Five Card Draw Playwright orbit. The ante is 10 and the
///     minimum bet is 10. Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from
///     that dealer. Betting and draws both start with the seat left of the dealer.
/// </summary>
public static class FiveCardDrawFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 10;
	public const int MinBet = 10;
	public const int OpeningPot = Ante * SeatCount;

	/// <summary>
	///     Clockwise action order, starting left of the dealer.
	/// </summary>
	public static readonly HoldEmPosition[] ActionOrder =
	[
		HoldEmPosition.SmallBlind,
		HoldEmPosition.BigBlind,
		HoldEmPosition.UnderTheGun,
		HoldEmPosition.Cutoff,
		HoldEmPosition.Button
	];

	public static FiveCardDrawHandScript Checkdown { get; } = new()
	{
		Name = "Checkdown with mixed draws",
		HoleCards = Holes(
			"Jc Jd 5d 6d 7d",
			"Ah Ad As Kc Kd",
			"2h 3h 4h 5h 7c",
			"9c 9d 8c 8d 3c",
			"Tc Td 4c 5c 6c"),
		Actions =
		[
			FiveCardDrawAction.Check(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Check(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Check(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Check(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Check(HoldEmPosition.Button),
			FiveCardDrawAction.StandPat(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Discard(HoldEmPosition.BigBlind, ["7c"], ["9h"]),
			FiveCardDrawAction.Discard(HoldEmPosition.UnderTheGun, ["3c"], ["2c"]),
			FiveCardDrawAction.Discard(HoldEmPosition.Cutoff, ["4c", "5c", "6c"], ["2d", "3d", "4d"]),
			FiveCardDrawAction.StandPat(HoldEmPosition.Button),
			FiveCardDrawAction.Check(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Check(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Check(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Check(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Check(HoldEmPosition.Button)
		],
		Nets = Antes("SmallBlind"),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = OpeningPot,
		WonByFold = false
	};

	public static FiveCardDrawHandScript FoldBeforeDraw { get; } = new()
	{
		Name = "Fold before the draw",
		HoleCards = Holes(
			"Ah Kh Qh Jh Th",
			"2c 3c 4c 5c 6c",
			"2d 3d 4d 5d 6d",
			"2h 3h 4h 5h 6h",
			"2s 3s 4s 5s 6s"),
		Actions =
		[
			FiveCardDrawAction.Fold(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Fold(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Fold(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Fold(HoldEmPosition.Cutoff)
		],
		Nets = Antes("Button"),
		Winner = HoldEmPosition.Button,
		AwardedAmount = OpeningPot,
		WonByFold = true
	};

	public static FiveCardDrawHandScript BetRaiseAndSecondBet { get; } = new()
	{
		Name = "Bet, raise, and a called second-round bet",
		HoleCards = Holes(
			"Qh Qd 8c 8d 3d",
			"Ah Ad As Kh Kd",
			"2c 3c 4c 5c 9h",
			"7c 8h 9c Tc Jc",
			"2h 3h 4h 5h 6s"),
		Actions =
		[
			FiveCardDrawAction.Bet(HoldEmPosition.SmallBlind, amount: 10, potAfter: 60),
			FiveCardDrawAction.Raise(HoldEmPosition.BigBlind, raiseTo: 30, potAfter: 90),
			FiveCardDrawAction.Fold(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Fold(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Call(HoldEmPosition.Button, callAmount: 30, potAfter: 120, streetBet: 30),
			FiveCardDrawAction.Call(HoldEmPosition.SmallBlind, callAmount: 20, potAfter: 140, streetBet: 30),
			FiveCardDrawAction.StandPat(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Discard(HoldEmPosition.BigBlind, ["9h", "5c"], ["6d", "7d"]),
			FiveCardDrawAction.Discard(HoldEmPosition.Button, ["3d"], ["4d"]),
			FiveCardDrawAction.Bet(HoldEmPosition.SmallBlind, amount: 20, potAfter: 160),
			FiveCardDrawAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 180, streetBet: 20),
			FiveCardDrawAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 200, streetBet: 20)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 140,
			[HoldEmPosition.BigBlind] = -60,
			[HoldEmPosition.Button] = -60,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 200,
		WonByFold = false
	};

	public static FiveCardDrawHandScript AceDiscardsFour { get; } = new()
	{
		Name = "Ace discards four",
		HoleCards = Holes(
			"Ts Td 5c 5s 4d",
			"Kc Kd Ks 9c 8c",
			"Qh Qd Qc 2h 3h",
			"Ah 2c 3c 4c 5d",
			"Jc Jd 6c 6d 2d"),
		Actions =
		[
			FiveCardDrawAction.Check(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Check(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Check(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Check(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Check(HoldEmPosition.Button),
			FiveCardDrawAction.StandPat(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Discard(HoldEmPosition.BigBlind, ["3h"], ["4h"]),
			FiveCardDrawAction.Discard(HoldEmPosition.UnderTheGun, ["2c", "3c", "4c", "5d"], ["6h", "7s", "8d", "9d"]),
			FiveCardDrawAction.StandPat(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Discard(HoldEmPosition.Button, ["4d", "5c"], ["3d", "3s"]),
			FiveCardDrawAction.Check(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Check(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Check(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Check(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Check(HoldEmPosition.Button)
		],
		Nets = Antes("SmallBlind"),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = OpeningPot,
		WonByFold = false
	};

	public static FiveCardDrawHandScript SecondRoundFolds { get; } = new()
	{
		Name = "Second-round bet and folds",
		HoleCards = Holes(
			"Js Ts 4s 3s 2c",
			"Ac Kc Qc Jc 9c",
			"2h 3h 4h 5h 7d",
			"6c 7c 8c 9h Th",
			"2d 3d 4d 5d 6d"),
		Actions =
		[
			FiveCardDrawAction.Check(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Check(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Check(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Check(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Check(HoldEmPosition.Button),
			FiveCardDrawAction.StandPat(HoldEmPosition.SmallBlind),
			FiveCardDrawAction.Discard(HoldEmPosition.BigBlind, ["2h", "3h", "4h"], ["8d", "9d", "Td"]),
			FiveCardDrawAction.StandPat(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Discard(HoldEmPosition.Cutoff, ["2d"], ["2s"]),
			FiveCardDrawAction.StandPat(HoldEmPosition.Button),
			FiveCardDrawAction.Bet(HoldEmPosition.SmallBlind, amount: 10, potAfter: 60),
			FiveCardDrawAction.Fold(HoldEmPosition.BigBlind),
			FiveCardDrawAction.Fold(HoldEmPosition.UnderTheGun),
			FiveCardDrawAction.Fold(HoldEmPosition.Cutoff),
			FiveCardDrawAction.Fold(HoldEmPosition.Button)
		],
		Nets = Antes("SmallBlind"),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 60,
		WonByFold = true
	};

	/// <summary>
	///     Five hands. The scenario deals hand <c>n</c> from seat <c>n</c>.
	/// </summary>
	public static IReadOnlyList<FiveCardDrawHandScript> Orbit { get; } =
	[
		Checkdown,
		FoldBeforeDraw,
		BetRaiseAndSecondBet,
		AceDiscardsFour,
		SecondRoundFolds
	];

	/// <summary>
	///     Draw actors in server order. Empty when the hand ends before the draw.
	///     The first actor is the first live seat left of the dealer, then clockwise.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DrawOrder(FiveCardDrawHandScript script)
	{
		var folded = new HashSet<HoldEmPosition>();
		foreach (var action in script.Actions)
		{
			if (action.Kind is FiveCardDrawActionKind.StandPat or FiveCardDrawActionKind.Discard)
				break;

			if (action.Kind == FiveCardDrawActionKind.Fold)
				folded.Add(action.Actor);
		}

		var live = ActionOrder.Where(position => !folded.Contains(position)).ToList();
		return live.Count < 2 ? [] : live;
	}

	/// <summary>
	///     Replacement cards in the order the draw consumes the stub.
	/// </summary>
	public static IReadOnlyList<string> DrawStub(FiveCardDrawHandScript script)
	{
		var drawsByActor = script.Actions
			.Where(action => action.Kind == FiveCardDrawActionKind.Discard)
			.ToDictionary(action => action.Actor, action => action.Draws ?? []);

		return DrawOrder(script)
			.Where(drawsByActor.ContainsKey)
			.SelectMany(position => drawsByActor[position])
			.ToArray();
	}

	public static IReadOnlyList<string> FinalHand(FiveCardDrawHandScript script, HoldEmPosition actor)
	{
		var discard = script.Actions.FirstOrDefault(action =>
			action.Actor == actor && action.Kind == FiveCardDrawActionKind.Discard);
		var thrown = new HashSet<string>(discard?.Discards ?? [], StringComparer.Ordinal);
		return script.HoleCards[actor]
			.Where(card => !thrown.Contains(card))
			.Concat(discard?.Draws ?? [])
			.ToArray();
	}

	private static Dictionary<HoldEmPosition, int> Antes(string winner)
	{
		var nets = ActionOrder.ToDictionary(position => position, _ => -Ante);
		nets[Enum.Parse<HoldEmPosition>(winner)] = Ante * (SeatCount - 1);
		return nets;
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
			[HoldEmPosition.Button] = Five(button),
			[HoldEmPosition.SmallBlind] = Five(smallBlind),
			[HoldEmPosition.BigBlind] = Five(bigBlind),
			[HoldEmPosition.UnderTheGun] = Five(underTheGun),
			[HoldEmPosition.Cutoff] = Five(cutoff)
		};
	}

	private static IReadOnlyList<string> Five(string cards) =>
		cards.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
