using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

public class HoldEmPotLayersTests
{
	private static readonly Guid AliceId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
	private static readonly Guid BobId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
	private static readonly Guid CarolId = new("cccccccc-cccc-cccc-cccc-ccccccccccc3");
	private static readonly Guid DougId = new("dddddddd-dddd-dddd-dddd-ddddddddddd4");

	[Fact]
	public void Build_WhenThreeAllInStacksDiffer_RefundsUncalledAndSplitsThePot()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(AliceId, "Alice", 1000, true, true),
			new(BobId, "Bob", 100, true, true),
			new(CarolId, "Carol", 300, true, true)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(700);
		result.UncalledPlayerId.Should().Be(AliceId);
		stakes[0].Contributed.Should().Be(1000);
		result.Layers.Should().HaveCount(2);

		var main = result.Layers[0];
		main.PotOrder.Should().Be(0);
		main.Amount.Should().Be(300);
		main.MaxContributionPerPlayer.Should().Be(100);
		main.Slices.Should().HaveCount(3);
		main.Slices.Should().OnlyContain(slice => slice.Amount == 100 && slice.Eligible);

		var side = result.Layers[1];
		side.PotOrder.Should().Be(1);
		side.Amount.Should().Be(400);
		side.MaxContributionPerPlayer.Should().Be(200);
		Slice(side, AliceId).Should().Be((200, true));
		Slice(side, CarolId).Should().Be((200, true));
		side.Slices.Should().NotContain(slice => slice.GamePlayerId == BobId);
		AssertConserved(stakes, result);
	}

	[Fact]
	public void Build_WhenHeadsUpShoveIsUncalled_RefundsTheExcessAndLeavesOnePot()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(AliceId, "Alice", 1000, true, true),
			new(BobId, "Bob", 100, true, true)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(900);
		result.UncalledPlayerId.Should().Be(AliceId);
		var layer = result.Layers.Should().ContainSingle().Subject;
		layer.PotOrder.Should().Be(0);
		layer.Amount.Should().Be(200);
		layer.MaxContributionPerPlayer.Should().Be(100);
		Slice(layer, AliceId).Should().Be((100, true));
		Slice(layer, BobId).Should().Be((100, true));
		AssertConserved(stakes, result);
	}

	[Fact]
	public void Build_WhenFoldedPlayerMatchedPartOfTheMainPot_KeepsThoseChipsInTheMainPot()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(AliceId, "Alice", 110, true, false),
			new(BobId, "Bob", 60, true, true),
			new(CarolId, "Carol", 110, true, false),
			new(DougId, "Doug", 10, false, false)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(0);
		result.UncalledPlayerId.Should().BeNull();
		result.Layers.Should().HaveCount(2);

		var main = result.Layers[0];
		main.Amount.Should().Be(190);
		main.Slices.Should().HaveCount(4);
		Slice(main, AliceId).Should().Be((60, true));
		Slice(main, BobId).Should().Be((60, true));
		Slice(main, CarolId).Should().Be((60, true));
		Slice(main, DougId).Should().Be((10, false));

		var side = result.Layers[1];
		side.Amount.Should().Be(100);
		Slice(side, AliceId).Should().Be((50, true));
		Slice(side, CarolId).Should().Be((50, true));
		side.Slices.Should().NotContain(slice => slice.GamePlayerId == BobId || slice.GamePlayerId == DougId);
		AssertConserved(stakes, result);
	}

	[Fact]
	public void Build_WhenTwoPlayersAreAllInForLess_EmitsALayerPerAllInLevel()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(AliceId, "A", 20, true, true),
			new(BobId, "B", 50, true, true),
			new(CarolId, "C", 100, true, false),
			new(DougId, "D", 100, true, false)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(0);
		result.Layers.Select(layer => (layer.PotOrder, layer.Amount, layer.MaxContributionPerPlayer))
			.Should().Equal((0, 80, 20), (1, 90, 30), (2, 100, 50));
		EligibleIds(result.Layers[0]).Should().Equal(AliceId, BobId, CarolId, DougId);
		EligibleIds(result.Layers[1]).Should().Equal(BobId, CarolId, DougId);
		EligibleIds(result.Layers[2]).Should().Equal(CarolId, DougId);
		AssertConserved(stakes, result);
	}

	[Fact]
	public void Build_WhenNobodyIsAllIn_EmitsOneMainPot()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(AliceId, "Alice", 40, true, false),
			new(BobId, "Bob", 40, true, false),
			new(CarolId, "Carol", 40, true, false)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(0);
		result.UncalledPlayerId.Should().BeNull();
		var layer = result.Layers.Should().ContainSingle().Subject;
		layer.PotOrder.Should().Be(0);
		layer.Amount.Should().Be(120);
		layer.MaxContributionPerPlayer.Should().Be(40);
		layer.Slices.Should().HaveCount(3);
		layer.Slices.Should().OnlyContain(slice => slice.Amount == 40 && slice.Eligible);
		AssertConserved(stakes, result);
	}

	[Fact]
	public void Build_WhenFoldedPlayerContributedMoreThanTheCap_PutsTheExcessInTheMainPot()
	{
		var stakes = new List<HoldEmPotLayers.Stake>
		{
			new(DougId, "Doug", 500, false, false),
			new(BobId, "Bob", 100, true, false),
			new(CarolId, "Carol", 100, true, false)
		};

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(0);
		var layer = result.Layers.Should().ContainSingle().Subject;
		layer.PotOrder.Should().Be(0);
		layer.Amount.Should().Be(700);
		Slice(layer, DougId).Should().Be((500, false));
		Slice(layer, BobId).Should().Be((100, true));
		Slice(layer, CarolId).Should().Be((100, true));
		AssertConserved(stakes, result);
	}

	private static (int Amount, bool Eligible) Slice(HoldEmPotLayers.Layer layer, Guid gamePlayerId)
	{
		var slice = layer.Slices.Single(candidate => candidate.GamePlayerId == gamePlayerId);
		return (slice.Amount, slice.Eligible);
	}

	private static IEnumerable<Guid> EligibleIds(HoldEmPotLayers.Layer layer)
	{
		return layer.Slices.Where(slice => slice.Eligible).Select(slice => slice.GamePlayerId);
	}

	private static void AssertConserved(
		IReadOnlyList<HoldEmPotLayers.Stake> stakes,
		HoldEmPotLayers.BuildResult result)
	{
		(result.Uncalled + result.Layers.Sum(layer => layer.Amount))
			.Should().Be(stakes.Sum(stake => stake.Contributed));

		foreach (var layer in result.Layers)
		{
			layer.Amount.Should().BeGreaterThan(0);
			layer.Slices.Sum(slice => slice.Amount).Should().Be(layer.Amount);
			layer.Slices.Should().Contain(slice => slice.Eligible);
		}
	}
}
