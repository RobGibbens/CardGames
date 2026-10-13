using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

public class SidePotInvariantTests
{
	private const int Seed = 20261012;

	[Fact]
	public void Build_SeededValidTables_ConserveChipsAndNestEligibility()
	{
		var random = new Random(Seed);
		for (var index = 0; index < 40; index++)
		{
			var stakes = ValidTable(random, index);
			var built = HoldEmPotLayers.Build(stakes);
			AssertConserved(stakes, built);

			var contesting = stakes.Where(stake => stake.Contesting).ToList();
			for (var layerIndex = 1; layerIndex < built.Layers.Count; layerIndex++)
			{
				var inner = Eligible(built.Layers[layerIndex]);
				var outer = Eligible(built.Layers[layerIndex - 1]);
				inner.Should().BeSubsetOf(outer);
			}

			foreach (var layer in built.Layers)
			{
				var eligibleIds = Eligible(layer);
				foreach (var shorter in contesting.Where(stake => eligibleIds.Contains(stake.GamePlayerId)))
				{
					foreach (var taller in contesting.Where(stake => stake.Contributed >= shorter.Contributed))
						eligibleIds.Should().Contain(taller.GamePlayerId);
				}

				layer.Slices.Where(slice => !slice.Eligible && !contesting.Any(stake => stake.GamePlayerId == slice.GamePlayerId))
					.Should().OnlyContain(slice => slice.Eligible == false);
			}

			built.Layers.SelectMany(layer => layer.Slices.Where(slice => slice.Eligible).Select(slice => slice.GamePlayerId))
				.Should().NotIntersectWith(stakes.Where(stake => !stake.Contesting).Select(stake => stake.GamePlayerId));
		}
	}

	[Fact]
	public void Build_WhenTheSameTableIsShuffled_KeepsAmountsAndEligibleSets()
	{
		var random = new Random(Seed);
		var stakes = ValidTable(random, hand: 7);
		var original = HoldEmPotLayers.Build(stakes);
		var shuffled = HoldEmPotLayers.Build(stakes.OrderBy(_ => random.Next()).ToList());

		shuffled.Uncalled.Should().Be(original.Uncalled);
		shuffled.UncalledPlayerId.Should().Be(original.UncalledPlayerId);
		shuffled.Layers.Select(layer => layer.Amount).Should().Equal(original.Layers.Select(layer => layer.Amount));
		for (var index = 0; index < original.Layers.Count; index++)
			Eligible(shuffled.Layers[index]).Should().BeEquivalentTo(Eligible(original.Layers[index]));
	}

	[Fact]
	public void Build_WhenCalledTwice_ReturnsTheSameLayers()
	{
		var stakes = ValidTable(new Random(Seed), hand: 3);
		var first = HoldEmPotLayers.Build(stakes);
		var second = HoldEmPotLayers.Build(stakes);

		second.Uncalled.Should().Be(first.Uncalled);
		second.Layers.Select(layer => (layer.Amount, layer.MaxContributionPerPlayer))
			.Should().Equal(first.Layers.Select(layer => (layer.Amount, layer.MaxContributionPerPlayer)));
		stakes.Sum(stake => stake.Contributed).Should().Be(first.Uncalled + first.Layers.Sum(layer => layer.Amount));
	}

	[Fact]
	public void Build_WhenAnEligiblePlayerContributesTenMore_AccountsForExactlyThoseChips()
	{
		var stakes = ValidTable(new Random(Seed), hand: 11);
		var before = HoldEmPotLayers.Build(stakes);
		var increased = stakes
			.Select((stake, index) => index == stakes.Count - 1
				? stake with { Contributed = stake.Contributed + 10 }
				: stake)
			.ToList();
		var after = HoldEmPotLayers.Build(increased);

		Accounted(before).Should().Be(stakes.Sum(stake => stake.Contributed));
		Accounted(after).Should().Be(Accounted(before) + 10);
	}

	private static int Accounted(HoldEmPotLayers.BuildResult result) =>
		result.Uncalled + result.Layers.Sum(layer => layer.Amount);

	private static HashSet<Guid> Eligible(HoldEmPotLayers.Layer layer) =>
		layer.Slices.Where(slice => slice.Eligible).Select(slice => slice.GamePlayerId).ToHashSet();

	private static void AssertConserved(
		IReadOnlyList<HoldEmPotLayers.Stake> stakes,
		HoldEmPotLayers.BuildResult result)
	{
		Accounted(result).Should().Be(stakes.Sum(stake => stake.Contributed));
		result.Uncalled.Should().BeGreaterThanOrEqualTo(0);
		result.Layers.Should().OnlyContain(layer => layer.Amount > 0);
		foreach (var layer in result.Layers)
		{
			layer.Slices.Sum(slice => slice.Amount).Should().Be(layer.Amount);
			layer.Slices.Should().Contain(slice => slice.Eligible);
			layer.Slices.Select(slice => slice.GamePlayerId).Should().OnlyHaveUniqueItems();
		}
	}

	private static List<HoldEmPotLayers.Stake> ValidTable(Random random, int hand)
	{
		var count = 2 + (hand + random.Next(0, 5)) % 5;
		var contributed = 0;
		var stakes = new List<HoldEmPotLayers.Stake>();
		for (var index = 0; index < count; index++)
		{
			contributed += 1 + random.Next(0, 20);
			var folded = index == 0 && count > 2 && random.Next(0, 4) == 0;
			var top = index == count - 1;
			stakes.Add(new HoldEmPotLayers.Stake(
				Guid.Parse($"00000000-0000-0000-0000-{index + 1 + hand * 10:x12}"),
				$"P{index}",
				contributed,
				!folded,
				!top && !folded));
		}

		// The largest tier is matched by a copy of the last contesting contribution
		// so the table is a legal called bet, not an unmatched shove.
		var leader = stakes.Last(stake => stake.Contesting);
		stakes.Add(leader with
		{
			GamePlayerId = Guid.Parse($"00000000-0000-0000-0000-{count + 1 + hand * 10:x12}"),
			Name = "Match"
		});
		return stakes;
	}
}
