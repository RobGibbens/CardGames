namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;

/// <summary>
///     Splits a Five Card Draw hand into pot layers from all-in levels.
///     Folded chips stay in the highest layer they reached.
/// </summary>
public static class FiveCardDrawSidePots
{
	public readonly record struct Stake(
		Guid GamePlayerId,
		int Contributed,
		bool HasFolded,
		bool IsAllIn);

	public readonly record struct Slice(Guid GamePlayerId, int Amount, bool Eligible);

	public readonly record struct Layer(
		int PotOrder,
		int Amount,
		int MaxContributionPerPlayer,
		IReadOnlyList<Slice> Slices);

	public static IReadOnlyList<Layer> Build(IReadOnlyList<Stake> stakes)
	{
		var allInLevels = stakes
			.Where(stake => stake.IsAllIn && !stake.HasFolded && stake.Contributed > 0)
			.Select(stake => stake.Contributed)
			.Distinct()
			.OrderBy(level => level)
			.ToList();

		if (allInLevels.Count == 0)
			return [];

		var layers = new List<Layer>();
		var previous = 0;

		foreach (var level in allInLevels)
		{
			var slices = SlicesThrough(stakes, previous, level);
			if (slices.Sum(slice => slice.Amount) > 0)
				layers.Add(new Layer(
					layers.Count,
					slices.Sum(slice => slice.Amount),
					level - previous,
					slices));

			previous = level;
		}

		if (stakes.Any(stake => stake.Contributed > previous))
		{
			var slices = SlicesAbove(stakes, previous);
			var largestContribution = stakes.Max(stake => stake.Contributed);
			layers.Add(new Layer(
				layers.Count,
				slices.Sum(slice => slice.Amount),
				largestContribution - previous,
				slices));
		}

		var layerTotal = layers.Sum(layer => layer.Amount);
		var contributedTotal = stakes.Sum(stake => stake.Contributed);
		if (layerTotal != contributedTotal)
			throw new InvalidOperationException(
				$"Side pot layers total {layerTotal}, but contributions total {contributedTotal}.");

		return layers;
	}

	private static List<Slice> SlicesThrough(IReadOnlyList<Stake> stakes, int previous, int level)
	{
		var slices = new List<Slice>();
		foreach (var stake in stakes)
		{
			if (stake.Contributed <= previous)
				continue;

			var amount = Math.Min(stake.Contributed, level) - previous;
			if (amount == 0)
				continue;

			slices.Add(new Slice(stake.GamePlayerId, amount, !stake.HasFolded));
		}

		return slices;
	}

	private static List<Slice> SlicesAbove(IReadOnlyList<Stake> stakes, int previous)
	{
		var slices = new List<Slice>();
		foreach (var stake in stakes)
		{
			if (stake.Contributed <= previous)
				continue;

			var amount = stake.Contributed - previous;
			if (amount == 0)
				continue;

			slices.Add(new Slice(stake.GamePlayerId, amount, !stake.HasFolded));
		}

		return slices;
	}
}
