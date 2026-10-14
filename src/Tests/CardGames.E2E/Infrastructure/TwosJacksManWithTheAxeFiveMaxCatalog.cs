namespace CardGames.E2E.Infrastructure;

public enum TwosJacksManWithTheAxeActionKind
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
public sealed record TwosJacksManWithTheAxeAction(
	HoldEmPosition Actor,
	TwosJacksManWithTheAxeActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	IReadOnlyList<string>? Discards = null,
	IReadOnlyList<string>? Draws = null)
{
	public static TwosJacksManWithTheAxeAction Fold(HoldEmPosition actor, int? potAfter = null) =>
		new(actor, TwosJacksManWithTheAxeActionKind.Fold, PotAfter: potAfter);

	public static TwosJacksManWithTheAxeAction Check(HoldEmPosition actor) =>
		new(actor, TwosJacksManWithTheAxeActionKind.Check);

	public static TwosJacksManWithTheAxeAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null) =>
		new(actor, TwosJacksManWithTheAxeActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet);

	public static TwosJacksManWithTheAxeAction Bet(HoldEmPosition actor, int amount, int potAfter) =>
		new(actor, TwosJacksManWithTheAxeActionKind.Bet, amount, potAfter, StreetBet: amount);

	public static TwosJacksManWithTheAxeAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, TwosJacksManWithTheAxeActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);

	public static TwosJacksManWithTheAxeAction StandPat(HoldEmPosition actor) =>
		new(actor, TwosJacksManWithTheAxeActionKind.StandPat);

	public static TwosJacksManWithTheAxeAction Discard(
		HoldEmPosition actor,
		IReadOnlyList<string> discards,
		IReadOnlyList<string> draws) =>
		new(actor, TwosJacksManWithTheAxeActionKind.Discard, Discards: discards, Draws: draws);
}

/// <summary>
///     A five-handed Twos, Jacks, Man with the Axe deal. Nets are the stack change when the hand
///     is over. <see cref="AwardedAmount" /> is the gross amount shown for the overlay winner.
///     <see cref="SevensAward" /> is the natural-sevens half when someone qualifies.
/// </summary>
public sealed record TwosJacksManWithTheAxeHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<TwosJacksManWithTheAxeAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public required int Pot { get; init; }

	public HoldEmPosition? SevensWinner { get; init; }

	public int SevensAward { get; init; }

	public bool WonByFold { get; init; }
}

/// <summary>
///     Known deals for the five-player Twos, Jacks, Man with the Axe Playwright orbit.
///     The ante is 10 and the minimum bet is 10 on both streets. Hand <c>n</c> is dealt by seat
///     <c>n</c>. Positions are offsets from that dealer. Betting and draws both start with the
///     seat left of the dealer. Every 2, every jack, and the king of diamonds are wild.
///     A natural pair of sevens takes half the pot unless the hand is won by fold.
/// </summary>
public static class TwosJacksManWithTheAxeFiveMaxCatalog
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

	public static TwosJacksManWithTheAxeHandScript AxeOnTheDraw { get; } = new()
	{
		Name = "Axe arrives on the draw and the sevens pool rolls over",
		HoleCards = Holes(
			"Tc Td Th 6c 5c",
			"Ah Ad As Ac 3c",
			"Kh Qh Qd Qc Qs",
			"9c 9d 9h 9s 8c",
			"8h 8d 6d 6h 4c"),
		Actions =
		[
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.SmallBlind, ["3c"], ["Kd"]),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.BigBlind, ["Kh"], ["3d"]),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.Cutoff, ["4c"], ["5d"]),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button)
		],
		Nets = Antes("SmallBlind"),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = OpeningPot,
		Pot = OpeningPot,
		WonByFold = false
	};

	public static TwosJacksManWithTheAxeHandScript FoldBeforeDraw { get; } = new()
	{
		Name = "Fold before the draw",
		HoleCards = Holes(
			"7h 7d 3c 4d 5h",
			"2c 3d 4h 5s 6s",
			"2d 3h 4s 5d 6d",
			"2h 3s 4c 6h 8s",
			"2s 9d 5c 9c 8d"),
		Actions =
		[
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Bet(HoldEmPosition.Button, amount: 10, potAfter: 60),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.Cutoff)
		],
		Nets = Antes("Button"),
		Winner = HoldEmPosition.Button,
		AwardedAmount = 60,
		Pot = 60,
		WonByFold = true
	};

	public static TwosJacksManWithTheAxeHandScript SevensSplitThePot { get; } = new()
	{
		Name = "Natural sevens take half and five aces take the other half",
		HoleCards = Holes(
			"Jh Ah Ad As Ac",
			"7h 7d 4c 5c 6c",
			"Qc Qd Qh Qs 9d",
			"9h 9s 8h 8d 3h",
			"Tc Td 6h 6s 3s"),
		Actions =
		[
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.UnderTheGun, ["3h"], ["4h"]),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 15,
			[HoldEmPosition.SmallBlind] = 15,
			[HoldEmPosition.BigBlind] = -Ante,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = OpeningPot / 2,
		Pot = OpeningPot,
		SevensWinner = HoldEmPosition.SmallBlind,
		SevensAward = OpeningPot / 2,
		WonByFold = false
	};

	public static TwosJacksManWithTheAxeHandScript AceDiscardsFour { get; } = new()
	{
		Name = "Ace discards four and four sevens scoop both halves",
		HoleCards = Holes(
			"Ts Td 5c 5h 4h",
			"7h 7d 7c 2s Kh",
			"Kc Ks 9h 8h 3d",
			"Ah 3c 4c 5d 6d",
			"Qc Qh 8s 6s 3s"),
		Actions =
		[
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.BigBlind, ["3d"], ["4s"]),
			TwosJacksManWithTheAxeAction.Discard(
				HoldEmPosition.UnderTheGun,
				["3c", "4c", "5d", "6d"],
				["9c", "8c", "4d", "5s"]),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.SmallBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Check(HoldEmPosition.Button)
		],
		Nets = Antes("SmallBlind"),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = OpeningPot,
		Pot = OpeningPot,
		SevensWinner = HoldEmPosition.SmallBlind,
		SevensAward = OpeningPot / 2,
		WonByFold = false
	};

	public static TwosJacksManWithTheAxeHandScript BetRaiseAndSplit { get; } = new()
	{
		Name = "Bet, raise, and a called second-round bet, then a split",
		HoleCards = Holes(
			"Jh Ah Ad As Ac",
			"Qh Qd 8c 8d 3d",
			"7h 7c 4c 5c 6c",
			"2c 3c 4h 5h 9h",
			"2h 3h 5d 6d 9c"),
		Actions =
		[
			TwosJacksManWithTheAxeAction.Bet(HoldEmPosition.SmallBlind, amount: 10, potAfter: 60),
			TwosJacksManWithTheAxeAction.Raise(HoldEmPosition.BigBlind, raiseTo: 30, potAfter: 90),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.UnderTheGun),
			TwosJacksManWithTheAxeAction.Fold(HoldEmPosition.Cutoff),
			TwosJacksManWithTheAxeAction.Call(HoldEmPosition.Button, callAmount: 30, potAfter: 120, streetBet: 30),
			TwosJacksManWithTheAxeAction.Call(HoldEmPosition.SmallBlind, callAmount: 20, potAfter: 140),
			TwosJacksManWithTheAxeAction.Discard(HoldEmPosition.SmallBlind, ["3d"], ["4d"]),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.BigBlind),
			TwosJacksManWithTheAxeAction.StandPat(HoldEmPosition.Button),
			TwosJacksManWithTheAxeAction.Bet(HoldEmPosition.SmallBlind, amount: 20, potAfter: 160),
			TwosJacksManWithTheAxeAction.Call(HoldEmPosition.BigBlind, callAmount: 20, potAfter: 180, streetBet: 20),
			TwosJacksManWithTheAxeAction.Call(HoldEmPosition.Button, callAmount: 20, potAfter: 200)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.Button] = 40,
			[HoldEmPosition.BigBlind] = 40,
			[HoldEmPosition.SmallBlind] = -60,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante
		},
		Winner = HoldEmPosition.Button,
		AwardedAmount = 100,
		Pot = 200,
		SevensWinner = HoldEmPosition.BigBlind,
		SevensAward = 100,
		WonByFold = false
	};

	/// <summary>
	///     Five hands. The scenario deals hand <c>n</c> from seat <c>n</c>.
	/// </summary>
	public static IReadOnlyList<TwosJacksManWithTheAxeHandScript> Orbit { get; } =
	[
		AxeOnTheDraw,
		FoldBeforeDraw,
		SevensSplitThePot,
		AceDiscardsFour,
		BetRaiseAndSplit
	];

	/// <summary>
	///     Draw actors in server order. Empty when the hand ends before the draw.
	///     The first actor is the first live seat left of the dealer, then clockwise.
	/// </summary>
	public static IReadOnlyList<HoldEmPosition> DrawOrder(TwosJacksManWithTheAxeHandScript script)
	{
		var folded = new HashSet<HoldEmPosition>();
		foreach (var action in script.Actions)
		{
			if (action.Kind is TwosJacksManWithTheAxeActionKind.StandPat or TwosJacksManWithTheAxeActionKind.Discard)
				break;

			if (action.Kind == TwosJacksManWithTheAxeActionKind.Fold)
				folded.Add(action.Actor);
		}

		var live = ActionOrder.Where(position => !folded.Contains(position)).ToList();
		return live.Count < 2 ? [] : live;
	}

	/// <summary>
	///     Replacement cards in the order the draw consumes the stub.
	/// </summary>
	public static IReadOnlyList<string> DrawStub(TwosJacksManWithTheAxeHandScript script)
	{
		var drawsByActor = script.Actions
			.Where(action => action.Kind == TwosJacksManWithTheAxeActionKind.Discard)
			.ToDictionary(action => action.Actor, action => action.Draws ?? []);

		return DrawOrder(script)
			.Where(drawsByActor.ContainsKey)
			.SelectMany(position => drawsByActor[position])
			.ToArray();
	}

	public static IReadOnlyList<string> FinalHand(TwosJacksManWithTheAxeHandScript script, HoldEmPosition actor)
	{
		var discard = script.Actions.FirstOrDefault(action =>
			action.Actor == actor && action.Kind == TwosJacksManWithTheAxeActionKind.Discard);
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
