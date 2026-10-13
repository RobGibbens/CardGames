namespace CardGames.E2E.Infrastructure;

public enum InBetweenActionKind
{
	Pass,
	Bet,
	BetPot
}

/// <summary>
///     One In-Between turn. <see cref="Boundaries" /> are dealt before the player acts.
///     <see cref="ThirdCard" /> is dealt only when the player bets.
///     Chip deltas are the table-stack change from the action itself, after the ante.
/// </summary>
public sealed record InBetweenAction(
	int Seat,
	InBetweenActionKind Kind,
	IReadOnlyList<string> Boundaries,
	int PotAfter,
	string? ThirdCard = null,
	int? Amount = null,
	bool ExpectAceChoice = false,
	bool? AceIsHigh = null,
	IReadOnlyDictionary<int, int>? ChipDeltas = null)
{
	public IReadOnlyDictionary<int, int> Deltas { get; } =
		ChipDeltas ?? new Dictionary<int, int>();

	public IReadOnlyList<string> DealtCards =>
		ThirdCard is null ? Boundaries : [..Boundaries, ThirdCard];

	public static InBetweenAction Pass(int seat, string first, string second, int potAfter) =>
		new(seat, InBetweenActionKind.Pass, [first, second], potAfter);

	public static InBetweenAction Bet(
		int seat,
		int amount,
		string first,
		string second,
		string third,
		int potAfter,
		IReadOnlyDictionary<int, int> chipDeltas,
		bool? aceIsHigh = null) =>
		new(
			seat,
			InBetweenActionKind.Bet,
			[first, second],
			potAfter,
			third,
			amount,
			ExpectAceChoice: aceIsHigh.HasValue,
			AceIsHigh: aceIsHigh,
			ChipDeltas: chipDeltas);

	public static InBetweenAction BetPot(
		int seat,
		string first,
		string second,
		string third,
		int potAfter,
		IReadOnlyDictionary<int, int> chipDeltas) =>
		new(
			seat,
			InBetweenActionKind.BetPot,
			[first, second],
			potAfter,
			third,
			ChipDeltas: chipDeltas);
}

/// <summary>
///     One In-Between hand. The dealer stays on seat 0, so the first actor is seat 1.
/// </summary>
public sealed class InBetweenHandScript
{
	public required string Name { get; init; }

	public required IReadOnlyList<InBetweenAction> Actions { get; init; }

	public IReadOnlyList<string> DealtCards =>
		Actions.SelectMany(action => action.DealtCards).ToArray();
}

/// <summary>
///     Running table stacks for an In-Between cash game.
///     Antes leave the stacks and return only when a bet wins them back.
/// </summary>
public sealed class InBetweenLedger
{
	private readonly int[] _stacks;

	public InBetweenLedger(IReadOnlyList<int> startingStacks)
	{
		_stacks = startingStacks.ToArray();
		StartingSum = _stacks.Sum();
	}

	public int StartingSum { get; }

	public IReadOnlyList<int> Stacks => _stacks;

	public void ApplyAnte(int ante, int seatCount)
	{
		if (seatCount != _stacks.Length)
			throw new ArgumentException("Ante must be posted by every seated player.", nameof(seatCount));

		for (var seat = 0; seat < _stacks.Length; seat++)
			_stacks[seat] -= ante;
	}

	public void Apply(InBetweenAction action)
	{
		foreach (var (seat, delta) in action.Deltas)
			_stacks[seat] += delta;
	}

	public void EnsureConserved(string handName, int pot)
	{
		var total = _stacks.Sum() + pot;
		if (total != StartingSum)
		{
			throw new InvalidOperationException(
				$"Hand '{handName}' changed the table chip total from {StartingSum} to {total}.");
		}
	}
}

/// <summary>
///     Four hands at a five-player In-Between table. Ante is 10, so each hand opens with a pot of 50.
///     The dealer stays on seat 0. Seat 1 acts first.
/// </summary>
public static class InBetweenFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int Ante = 10;
	public const int OpeningPot = Ante * SeatCount;

	/// <summary>
	///     Pass, ace-low loss, ace-high win, post, and a full-pot win that empties the pot.
	/// </summary>
	public static InBetweenHandScript RuleSweep { get; } = new()
	{
		Name = "Rule sweep",
		Actions =
		[
			// Second card is an ace, which is always high and does not ask for a choice.
			InBetweenAction.Pass(seat: 1, "2h", "Ah", potAfter: 50),
			InBetweenAction.Bet(
				seat: 2, amount: 10, "Ac", "7h", "Kh", potAfter: 60,
				chipDeltas: Delta(2, -10), aceIsHigh: false),
			InBetweenAction.Bet(
				seat: 3, amount: 10, "Ad", "7d", "9h", potAfter: 50,
				chipDeltas: Delta(3, 10), aceIsHigh: true),
			InBetweenAction.Bet(
				seat: 4, amount: 10, "5h", "9d", "5d", potAfter: 70,
				chipDeltas: Delta(4, -20)),
			InBetweenAction.Pass(seat: 0, "3h", "6h", potAfter: 70),
			InBetweenAction.BetPot(
				seat: 1, "2d", "8d", "5c", potAfter: 0,
				chipDeltas: Delta(1, 70))
		]
	};

	/// <summary>
	///     Five passes finish the first orbit. The next player then bets the pot and wins it.
	/// </summary>
	public static InBetweenHandScript EmptyThePot { get; } = new()
	{
		Name = "Empty the pot",
		Actions =
		[
			InBetweenAction.Pass(seat: 1, "2h", "8h", potAfter: OpeningPot),
			InBetweenAction.Pass(seat: 2, "3h", "9h", potAfter: OpeningPot),
			InBetweenAction.Pass(seat: 3, "4h", "6h", potAfter: OpeningPot),
			InBetweenAction.Pass(seat: 4, "5h", "7h", potAfter: OpeningPot),
			InBetweenAction.Pass(seat: 0, "2d", "9d", potAfter: OpeningPot),
			InBetweenAction.BetPot(
				seat: 1, "3d", "8d", "5d", potAfter: 0,
				chipDeltas: Delta(1, OpeningPot))
		]
	};

	public static IReadOnlyList<InBetweenHandScript> Hands { get; } =
	[
		RuleSweep,
		EmptyThePot,
		EmptyThePot,
		EmptyThePot
	];

	private static Dictionary<int, int> Delta(int seat, int amount) =>
		new() { [seat] = amount };
}
