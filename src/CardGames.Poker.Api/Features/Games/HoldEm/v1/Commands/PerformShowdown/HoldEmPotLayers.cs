namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

/// <summary>
///     Splits a Hold'em hand's contributions into an uncalled refund and the pots contested at showdown.
/// </summary>
public static class HoldEmPotLayers
{
	public readonly record struct Stake(
		Guid GamePlayerId,
		string Name,
		int Contributed,
		bool Contesting,
		bool AllIn);

	public readonly record struct Slice(
		Guid GamePlayerId,
		string Name,
		int Amount,
		bool Eligible);

	public readonly record struct Layer(
		int PotOrder,
		int Amount,
		int MaxContributionPerPlayer,
		IReadOnlyList<Slice> Slices);

	public readonly record struct BuildResult(
		int Uncalled,
		Guid? UncalledPlayerId,
		IReadOnlyList<Layer> Layers);

	public static BuildResult Build(IReadOnlyList<Stake> stakes)
	{
		var contesting = stakes.Where(stake => stake.Contesting).ToList();
		var uncalled = 0;
		Guid? uncalledPlayerId = null;

		if (contesting.Count >= 2)
		{
			// A folded caller still matches a bet. Refund only the portion that
			// nobody — including folded players — put in.
			var ranked = contesting
				.OrderByDescending(stake => stake.Contributed)
				.ThenBy(stake => stake.GamePlayerId)
				.ToList();
			var highest = ranked[0].Contributed;
			if (ranked[1].Contributed < highest)
			{
				var matchedByAnyoneElse = stakes
					.Where(stake => stake.GamePlayerId != ranked[0].GamePlayerId)
					.Select(stake => stake.Contributed)
					.DefaultIfEmpty(0)
					.Max();
				uncalled = Math.Max(0, highest - matchedByAnyoneElse);
				if (uncalled > 0)
					uncalledPlayerId = ranked[0].GamePlayerId;
			}
		}

		if (contesting.Count == 0)
			return new BuildResult(0, null, []);

		var working = stakes.Select(stake => new WorkingStake
		{
			GamePlayerId = stake.GamePlayerId,
			Name = stake.Name,
			Contesting = stake.Contesting,
			AllIn = stake.AllIn,
			Contributed = stake.Contributed
		}).ToList();

		if (uncalled > 0 && uncalledPlayerId is Guid refundedId)
			working.First(stake => stake.GamePlayerId == refundedId).Contributed -= uncalled;

		var cap = working.Where(stake => stake.Contesting).Max(stake => stake.Contributed);
		var deadMoney = 0;
		foreach (var stake in working)
		{
			stake.Matched = Math.Min(stake.Contributed, cap);
			stake.Excess = stake.Contributed - stake.Matched;
			deadMoney += stake.Excess;
		}

		var allInLevels = working
			.Where(stake => stake.Contesting && stake.AllIn && stake.Matched > 0)
			.Select(stake => stake.Matched)
			.Distinct()
			.OrderBy(level => level)
			.ToList();

		if (allInLevels.Count == 0)
			return new BuildResult(uncalled, uncalledPlayerId, BuildFlatLayer(working, cap, deadMoney));

		var layers = new List<Layer>();
		var previous = 0;
		foreach (var level in allInLevels)
		{
			var slices = working
				.Select(stake => (stake, Amount: Math.Max(0, Math.Min(stake.Matched, level) - previous)))
				.ToList();
			var eligibleIds = working
				.Where(stake => stake.Contesting && stake.Matched >= level)
				.Select(stake => stake.GamePlayerId)
				.ToHashSet();
			Emit(layers, level - previous, slices, eligibleIds);
			previous = level;
		}

		if (working.Any(stake => stake.Matched > previous))
		{
			var slices = working
				.Select(stake => (stake, Amount: Math.Max(0, stake.Matched - previous)))
				.ToList();
			var eligibleIds = working
				.Where(stake => stake.Contesting && stake.Matched > previous)
				.Select(stake => stake.GamePlayerId)
				.ToHashSet();
			var maxMatched = working.Max(stake => stake.Matched);
			Emit(layers, maxMatched - previous, slices, eligibleIds);
		}

		if (deadMoney > 0 && layers.All(layer => layer.PotOrder != 0))
			return new BuildResult(uncalled, uncalledPlayerId, []);

		AddDeadMoney(layers, working, deadMoney);
		return new BuildResult(uncalled, uncalledPlayerId, layers);
	}

	private static IReadOnlyList<Layer> BuildFlatLayer(List<WorkingStake> working, int cap, int deadMoney)
	{
		var amount = working.Sum(stake => stake.Matched) + deadMoney;
		if (amount <= 0)
			return [];

		var slices = new List<Slice>();
		if (cap == 0 && deadMoney > 0)
		{
			slices.AddRange(working
				.Where(stake => stake.Contesting)
				.Select(stake => new Slice(stake.GamePlayerId, stake.Name, 0, true)));
			slices.AddRange(working
				.Where(stake => !stake.Contesting && stake.Excess > 0)
				.Select(stake => new Slice(stake.GamePlayerId, stake.Name, stake.Excess, false)));
		}
		else
		{
			slices.AddRange(working
				.Where(stake => stake.Matched > 0)
				.Select(stake => new Slice(stake.GamePlayerId, stake.Name, stake.Matched, stake.Contesting)));

			foreach (var stake in working.Where(stake => stake.Excess > 0))
			{
				var index = slices.FindIndex(slice => slice.GamePlayerId == stake.GamePlayerId);
				if (index >= 0)
					slices[index] = slices[index] with { Amount = slices[index].Amount + stake.Excess };
				else
					slices.Add(new Slice(stake.GamePlayerId, stake.Name, stake.Excess, false));
			}
		}

		if (!slices.Any(slice => slice.Eligible))
			return [];

		return [new Layer(0, amount, cap, slices)];
	}

	private static void Emit(
		List<Layer> layers,
		int maxContributionPerPlayer,
		List<(WorkingStake Stake, int Amount)> slices,
		HashSet<Guid> eligibleIds)
	{
		var amount = slices.Sum(slice => slice.Amount);
		if (amount <= 0)
			return;

		var published = slices
			.Where(slice => slice.Amount > 0)
			.Select(slice => new Slice(
				slice.Stake.GamePlayerId,
				slice.Stake.Name,
				slice.Amount,
				eligibleIds.Contains(slice.Stake.GamePlayerId)))
			.ToList();

		if (!published.Any(slice => slice.Eligible))
			return;

		layers.Add(new Layer(layers.Count, amount, maxContributionPerPlayer, published));
	}

	private static void AddDeadMoney(List<Layer> layers, List<WorkingStake> working, int deadMoney)
	{
		if (deadMoney == 0)
			return;

		var index = layers.FindIndex(layer => layer.PotOrder == 0);
		if (index < 0)
			return;

		var main = layers[index];
		var slices = main.Slices.ToList();
		foreach (var stake in working.Where(stake => stake.Excess > 0))
		{
			var sliceIndex = slices.FindIndex(slice => slice.GamePlayerId == stake.GamePlayerId);
			if (sliceIndex >= 0)
				slices[sliceIndex] = slices[sliceIndex] with { Amount = slices[sliceIndex].Amount + stake.Excess };
			else
				slices.Add(new Slice(stake.GamePlayerId, stake.Name, stake.Excess, false));
		}

		layers[index] = main with { Amount = main.Amount + deadMoney, Slices = slices };
	}

	private sealed class WorkingStake
	{
		public required Guid GamePlayerId { get; init; }
		public required string Name { get; init; }
		public required bool Contesting { get; init; }
		public required bool AllIn { get; init; }
		public int Contributed { get; set; }
		public int Matched { get; set; }
		public int Excess { get; set; }
	}
}
