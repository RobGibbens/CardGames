namespace CardGames.E2E.Infrastructure;

public enum GoodBadUglyActionKind
{
	Fold,
	Check,
	Call,
	Bet,
	RaiseTo
}

/// <summary>
///     One betting action. <see cref="CommunityCount" /> is how many table cards are face up
///     before the action. <see cref="HoleCards" /> is the actor's remaining hole cards when
///     The Bad has already discarded.
/// </summary>
public sealed record GoodBadUglyAction(
	HoldEmPosition Actor,
	GoodBadUglyActionKind Kind,
	int? Amount = null,
	int? PotAfter = null,
	int? CallAmount = null,
	int? StreetBet = null,
	int? CommunityCount = null,
	IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? HoleCards = null)
{
	public static GoodBadUglyAction Fold(HoldEmPosition actor) =>
		new(actor, GoodBadUglyActionKind.Fold);

	public static GoodBadUglyAction Check(
		HoldEmPosition actor,
		int? communityCount = null,
		IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>>? holeCards = null) =>
		new(actor, GoodBadUglyActionKind.Check, CommunityCount: communityCount, HoleCards: holeCards);

	public static GoodBadUglyAction Call(
		HoldEmPosition actor,
		int callAmount,
		int potAfter,
		int? streetBet = null,
		int? communityCount = null) =>
		new(actor, GoodBadUglyActionKind.Call, PotAfter: potAfter, CallAmount: callAmount, StreetBet: streetBet,
			CommunityCount: communityCount);

	public static GoodBadUglyAction Bet(
		HoldEmPosition actor,
		int amount,
		int potAfter,
		int? communityCount = null) =>
		new(actor, GoodBadUglyActionKind.Bet, amount, potAfter, StreetBet: amount, CommunityCount: communityCount);

	public static GoodBadUglyAction Raise(HoldEmPosition actor, int raiseTo, int potAfter) =>
		new(actor, GoodBadUglyActionKind.RaiseTo, raiseTo, potAfter, StreetBet: raiseTo);
}

/// <summary>
///     A five-handed Good, Bad, and Ugly deal. Community cards are The Good, The Bad, and The Ugly.
///     Nets are the stack change when the hand is over. <see cref="AwardedAmount" /> is the gross pot
///     shown for the winner. <see cref="EvaluatedDescription" /> is the wild-card hand name.
///     <see cref="WinnerDescription" /> is the showdown callout, which says the hand is dead when
///     The Ugly eliminated the winner and nobody else was live.
/// </summary>
public sealed record GoodBadUglyHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> HoleCards { get; init; }

	public required IReadOnlyList<string> Community { get; init; }

	public required IReadOnlyList<GoodBadUglyAction> Actions { get; init; }

	public required IReadOnlyDictionary<HoldEmPosition, int> Nets { get; init; }

	public required HoldEmPosition Winner { get; init; }

	public required int AwardedAmount { get; init; }

	public bool WonByFold { get; init; }

	public string? WinnerDescription { get; init; }

	public string? EvaluatedDescription { get; init; }

	public IReadOnlyList<HoldEmPosition> Eliminated { get; init; } = [];
}

public sealed class GoodBadUglyLedger
{
	private readonly int[] _stacks;

	public GoodBadUglyLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void Apply(GoodBadUglyHandScript script, int dealerSeat)
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
///     Known deals for the five-player Good, Bad, and Ugly Playwright orbit.
///     The ante is 10, the small bet is 10, and the big bet is 20. There is no bring-in.
///     Hand <c>n</c> is dealt by seat <c>n</c>. Positions are offsets from that dealer.
///     Betting starts with the seat left of the dealer.
/// </summary>
public static class GoodBadUglyFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 10;
	public const int SmallBet = 10;
	public const int BigBet = 20;
	public const int OpeningPot = Ante * SeatCount;

	public static readonly HoldEmPosition[] ActionOrder =
	[
		HoldEmPosition.SmallBlind,
		HoldEmPosition.BigBlind,
		HoldEmPosition.UnderTheGun,
		HoldEmPosition.Cutoff,
		HoldEmPosition.Button
	];

	public static GoodBadUglyHandScript FoldBeforeReveal { get; } = new()
	{
		Name = "Fold before any reveal",
		HoleCards = Holes("5h 6h 7h 8h", "9h Th Jh Qh", "5d 6d 7d 8d", "9d Td Jd Qd", "5s 6s 7s 8s"),
		Community = ["2c", "3d", "4s"],
		Actions =
		[
			GoodBadUglyAction.Bet(HoldEmPosition.SmallBlind, amount: SmallBet, potAfter: 60),
			GoodBadUglyAction.Fold(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Fold(HoldEmPosition.UnderTheGun),
			GoodBadUglyAction.Fold(HoldEmPosition.Cutoff),
			GoodBadUglyAction.Fold(HoldEmPosition.Button)
		],
		Nets = AnteNets(HoldEmPosition.SmallBlind, winnerNet: 40),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 60,
		WonByFold = true
	};

	public static GoodBadUglyHandScript WildRoyal { get; } = new()
	{
		Name = "The Good makes a royal",
		HoleCards = Holes("Ah Kh Qh Jh", "Ad Ac As Kd", "5h 6h 7c 8c", "5d 6d 7s 8s", "9c Tc Jc Qc"),
		Community = ["2c", "3d", "4s"],
		Actions =
		[
			..BetAndFoldThird(),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 1),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 2),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 3),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind)
		],
		Nets = HeadsUpNets(HoldEmPosition.SmallBlind, winnerNet: 50, loserNet: -20),
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 70,
		WinnerDescription = "Royal flush",
		EvaluatedDescription = "Royal flush"
	};

	public static GoodBadUglyHandScript BadDiscardsTheQueen { get; } = new()
	{
		Name = "The Bad throws away the queen",
		HoleCards = Holes("Ah Kh Qh Jh", "Ad Ac As Kd", "5h 6h 7c 8c", "5d 6d 7s 8s", "9c Tc Jc 9s"),
		Community = ["2c", "Qd", "4s"],
		Actions =
		[
			..BetAndFoldThird(),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 1),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Check(
				HoldEmPosition.SmallBlind,
				communityCount: 2,
				holeCards: new Dictionary<HoldEmPosition, IReadOnlyList<string>>
				{
					[HoldEmPosition.SmallBlind] = ["Ah", "Kh", "Jh"]
				}),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 3),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind)
		],
		Nets = HeadsUpNets(HoldEmPosition.BigBlind, winnerNet: 50, loserNet: -20),
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 70,
		WinnerDescription = "Four of a kind, Aces",
		EvaluatedDescription = "Four of a kind, Aces"
	};

	public static GoodBadUglyHandScript UglyKillsTheRoyal { get; } = new()
	{
		Name = "The Ugly kills the royal",
		HoleCards = Holes("As Kh Qh Jh", "9h 8h 7h 6h", "5c 5d 6c 7d", "4c 4d 5s 6s", "9c Tc Jc Qc"),
		Community = ["2c", "3d", "Ah"],
		Actions =
		[
			..BetAndFoldThird(),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 1),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 2),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Bet(HoldEmPosition.SmallBlind, amount: BigBet, potAfter: 90, communityCount: 3),
			GoodBadUglyAction.Call(HoldEmPosition.BigBlind, callAmount: BigBet, potAfter: 110)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.BigBlind] = 70,
			[HoldEmPosition.SmallBlind] = -40,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante,
			[HoldEmPosition.Button] = -Ante
		},
		Winner = HoldEmPosition.BigBlind,
		AwardedAmount = 110,
		WinnerDescription = "Straight flush to the Ten",
		EvaluatedDescription = "Straight flush to the Ten",
		Eliminated = [HoldEmPosition.SmallBlind]
	};

	public static GoodBadUglyHandScript AllDeadBestHandWins { get; } = new()
	{
		Name = "Every remaining player is dead",
		HoleCards = Holes("Ah Kh Qh Jh", "Kd 9d 8d 7d", "5h 6h 7c 8c", "5d 6d 7s 8s", "9c Tc Jc Qc"),
		Community = ["2c", "3d", "Kc"],
		Actions =
		[
			..BetAndFoldThird(),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 1),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind),
			GoodBadUglyAction.Bet(HoldEmPosition.SmallBlind, amount: BigBet, potAfter: 90, communityCount: 2),
			GoodBadUglyAction.Raise(HoldEmPosition.BigBlind, raiseTo: 40, potAfter: 130),
			GoodBadUglyAction.Call(HoldEmPosition.SmallBlind, callAmount: 20, potAfter: 150),
			GoodBadUglyAction.Check(HoldEmPosition.SmallBlind, communityCount: 3),
			GoodBadUglyAction.Check(HoldEmPosition.BigBlind)
		],
		Nets = new Dictionary<HoldEmPosition, int>
		{
			[HoldEmPosition.SmallBlind] = 90,
			[HoldEmPosition.BigBlind] = -60,
			[HoldEmPosition.UnderTheGun] = -Ante,
			[HoldEmPosition.Cutoff] = -Ante,
			[HoldEmPosition.Button] = -Ante
		},
		Winner = HoldEmPosition.SmallBlind,
		AwardedAmount = 150,
		WinnerDescription = "Dead Hand (The Ugly)",
		EvaluatedDescription = "Royal flush",
		Eliminated = [HoldEmPosition.SmallBlind, HoldEmPosition.BigBlind]
	};

	public static IReadOnlyList<GoodBadUglyHandScript> Orbit { get; } =
	[
		FoldBeforeReveal,
		WildRoyal,
		BadDiscardsTheQueen,
		UglyKillsTheRoyal,
		AllDeadBestHandWins
	];

	/// <summary>
	///     Fold is offered only when there is a bet to call, so Third Street opens with a small bet.
	/// </summary>
	private static IReadOnlyList<GoodBadUglyAction> BetAndFoldThird() =>
	[
		GoodBadUglyAction.Bet(HoldEmPosition.SmallBlind, amount: SmallBet, potAfter: 60),
		GoodBadUglyAction.Call(HoldEmPosition.BigBlind, callAmount: SmallBet, potAfter: 70, streetBet: SmallBet),
		GoodBadUglyAction.Fold(HoldEmPosition.UnderTheGun),
		GoodBadUglyAction.Fold(HoldEmPosition.Cutoff),
		GoodBadUglyAction.Fold(HoldEmPosition.Button)
	];

	private static Dictionary<HoldEmPosition, int> AnteNets(HoldEmPosition winner, int winnerNet)
	{
		var nets = ActionOrder.ToDictionary(position => position, _ => -Ante);
		nets[winner] = winnerNet;
		return nets;
	}

	private static Dictionary<HoldEmPosition, int> HeadsUpNets(HoldEmPosition winner, int winnerNet, int loserNet)
	{
		var nets = ActionOrder.ToDictionary(position => position, _ => -Ante);
		nets[HoldEmPosition.SmallBlind] = winner == HoldEmPosition.SmallBlind ? winnerNet : loserNet;
		nets[HoldEmPosition.BigBlind] = winner == HoldEmPosition.BigBlind ? winnerNet : loserNet;
		return nets;
	}

	private static IReadOnlyDictionary<HoldEmPosition, IReadOnlyList<string>> Holes(
		string smallBlind,
		string bigBlind,
		string underTheGun,
		string cutoff,
		string button) =>
		new Dictionary<HoldEmPosition, IReadOnlyList<string>>
		{
			[HoldEmPosition.SmallBlind] = Split(smallBlind),
			[HoldEmPosition.BigBlind] = Split(bigBlind),
			[HoldEmPosition.UnderTheGun] = Split(underTheGun),
			[HoldEmPosition.Cutoff] = Split(cutoff),
			[HoldEmPosition.Button] = Split(button)
		};

	private static string[] Split(string cards) => cards.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
