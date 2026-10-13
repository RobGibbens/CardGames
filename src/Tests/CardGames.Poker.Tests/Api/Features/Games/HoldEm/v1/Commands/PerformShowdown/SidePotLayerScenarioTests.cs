using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;

/// <summary>
///     Pot construction only. Winner selection lives in <see cref="SidePotAwardScenarioTests" />.
///     Expected amounts are written out; nothing here calls <c>Build</c> to invent them.
/// </summary>
public class SidePotLayerScenarioTests
{
	private static readonly IReadOnlyDictionary<string, Guid> Ids =
		new Dictionary<string, Guid>
		{
			["Ann"] = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
			["Bob"] = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
			["Cal"] = new("cccccccc-cccc-cccc-cccc-ccccccccccc3"),
			["Dee"] = new("dddddddd-dddd-dddd-dddd-ddddddddddd4"),
			["Eve"] = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee5"),
			["Fay"] = new("ffffffff-ffff-ffff-ffff-fffffffffff6")
		};

	public static TheoryData<string> CaseIds()
	{
		var data = new TheoryData<string>();
		foreach (var caseId in Cases.Keys)
			data.Add(caseId);
		return data;
	}

	[Theory]
	[MemberData(nameof(CaseIds))]
	public void Build_ExplicitContributionTiers_MatchAmountsEligibilityAndRefunds(string caseId)
	{
		var scenario = Cases[caseId];
		var stakes = scenario.Players
			.Select(player => new HoldEmPotLayers.Stake(
				Ids[player.Name],
				player.Name,
				player.Contribution,
				player.Contesting,
				player.AllIn))
			.ToList();

		var result = HoldEmPotLayers.Build(stakes);

		result.Uncalled.Should().Be(scenario.Uncalled, caseId);
		if (scenario.Refunded is null)
			result.UncalledPlayerId.Should().BeNull();
		else
			result.UncalledPlayerId.Should().Be(Ids[scenario.Refunded]);

		result.Layers.Select(layer => layer.Amount).Should().Equal(scenario.Layers.Select(layer => layer.Amount));
		for (var index = 0; index < scenario.Layers.Length; index++)
		{
			var actual = result.Layers[index];
			var expected = scenario.Layers[index];
			actual.PotOrder.Should().Be(index);
			actual.Amount.Should().Be(expected.Amount);
			actual.MaxContributionPerPlayer.Should().Be(expected.Width);
			actual.Slices.Where(slice => slice.Eligible).Select(slice => slice.Name)
				.Should().BeEquivalentTo(expected.Eligible);
			actual.Slices.Sum(slice => slice.Amount).Should().Be(expected.Amount);
			foreach (var (name, amount) in expected.Ineligible)
			{
				var slice = actual.Slices.Should().ContainSingle(candidate => candidate.Name == name).Subject;
				slice.Amount.Should().Be(amount);
				slice.Eligible.Should().BeFalse();
			}

			foreach (var name in expected.Eligible)
				actual.Slices.Should().Contain(slice => slice.Name == name && slice.Eligible);
		}

		(result.Uncalled + result.Layers.Sum(layer => layer.Amount))
			.Should().Be(scenario.Players.Sum(player => player.Contribution));
		result.Layers.Should().OnlyContain(layer => layer.Amount > 0);
	}

	private static Dictionary<string, BuildCase> Cases { get; } = new()
	{
		["01-two-equal"] = Layer(
			P("Ann", 100), P("Bob", 100),
			0, null,
			L(200, 100, "Ann", "Bob")),
		["02-three-equal"] = Layer(
			P("Ann", 40), P("Bob", 40), P("Cal", 40),
			0, null,
			L(120, 40, "Ann", "Bob", "Cal")),
		["05-last-player-takes-folded-chips"] = Layer(
			P("Ann", 80), P("Bob", 40, contesting: false), P("Cal", 25, contesting: false),
			0, null,
			L(145, 80, ["Ann"], ("Bob", 40), ("Cal", 25))),
		["07-one-short-all-in"] = Layer(
			P("Ann", 30, allIn: true), P("Bob", 100), P("Cal", 100),
			0, null,
			L(90, 30, "Ann", "Bob", "Cal"),
			L(140, 70, "Bob", "Cal")),
		["08-fixture-1"] = Layer(
			P("Ann", 100, allIn: true), P("Bob", 200), P("Cal", 200),
			0, null,
			L(300, 100, "Ann", "Bob", "Cal"),
			L(200, 100, "Bob", "Cal")),
		["09-short-called-by-two"] = Layer(
			P("Ann", 40, allIn: true), P("Bob", 90), P("Cal", 90), P("Dee", 90),
			0, null,
			L(160, 40, "Ann", "Bob", "Cal", "Dee"),
			L(150, 50, "Bob", "Cal", "Dee")),
		["10-all-in-then-raise"] = Layer(
			P("Ann", 40, allIn: true), P("Bob", 120), P("Cal", 120),
			0, null,
			L(120, 40, "Ann", "Bob", "Cal"),
			L(160, 80, "Bob", "Cal")),
		["16-three-amounts-top-is-refund"] = Layer(
			P("Ann", 20, allIn: true), P("Bob", 50, allIn: true), P("Cal", 100, allIn: true),
			50, "Cal",
			L(60, 20, "Ann", "Bob", "Cal"),
			L(60, 30, "Bob", "Cal")),
		["17-four-different-levels"] = Layer(
			P("Ann", 10, allIn: true), P("Bob", 30, allIn: true), P("Cal", 60, allIn: true), P("Dee", 100, allIn: true),
			40, "Dee",
			L(40, 10, "Ann", "Bob", "Cal", "Dee"),
			L(60, 20, "Bob", "Cal", "Dee"),
			L(60, 30, "Cal", "Dee")),
		["18-six-players"] = Layer(
			P("Ann", 10, allIn: true), P("Bob", 20, allIn: true), P("Cal", 40, allIn: true),
			P("Dee", 40), P("Eve", 70), P("Fay", 70),
			0, null,
			L(60, 10, "Ann", "Bob", "Cal", "Dee", "Eve", "Fay"),
			L(50, 10, "Bob", "Cal", "Dee", "Eve", "Fay"),
			L(80, 20, "Cal", "Dee", "Eve", "Fay"),
			L(60, 30, "Eve", "Fay")),
		["19-shared-levels"] = Layer(
			P("Ann", 50, allIn: true), P("Bob", 50, allIn: true), P("Cal", 100, allIn: true),
			P("Dee", 200), P("Eve", 200),
			0, null,
			L(250, 50, "Ann", "Bob", "Cal", "Dee", "Eve"),
			L(150, 50, "Cal", "Dee", "Eve"),
			L(200, 100, "Dee", "Eve")),
		["20-mixed-all-ins"] = Layer(
			P("Ann", 40, allIn: true), P("Bob", 90, allIn: true), P("Cal", 150), P("Dee", 150),
			0, null,
			L(160, 40, "Ann", "Bob", "Cal", "Dee"),
			L(150, 50, "Bob", "Cal", "Dee"),
			L(120, 60, "Cal", "Dee")),
		["28-one-chip-increment"] = Layer(
			P("Ann", 1, allIn: true), P("Bob", 2), P("Cal", 2),
			0, null,
			L(3, 1, "Ann", "Bob", "Cal"),
			L(2, 1, "Bob", "Cal")),
		["28-large-equal"] = Layer(
			P("Ann", 100), P("Bob", 100), P("Cal", 100),
			0, null,
			L(300, 100, "Ann", "Bob", "Cal")),
		["29-fixture-4-uncalled"] = Layer(
			P("Ann", 150, allIn: true), P("Bob", 100, allIn: true),
			50, "Ann",
			L(200, 100, "Ann", "Bob")),
		["30-folded-after-matching-main"] = Layer(
			P("Ann", 100), P("Bob", 100), P("Cal", 100, contesting: false),
			0, null,
			L(300, 100, ["Ann", "Bob"], ("Cal", 100))),
		["31-folded-after-a-side-pot"] = Layer(
			P("Ann", 50, allIn: true), P("Bob", 100, contesting: false), P("Cal", 100),
			0, null,
			L(150, 50, ["Ann", "Cal"], ("Bob", 50)),
			L(100, 50, ["Cal"], ("Bob", 50))),
		["33-folded-above-a-shorter-caller"] = Layer(
			P("Ann", 40, allIn: true), P("Bob", 100), P("Cal", 100), P("Dee", 200, contesting: false),
			0, null,
			L(260, 40, ["Ann", "Bob", "Cal"], ("Dee", 140)),
			L(180, 60, ["Bob", "Cal"], ("Dee", 60))),
		["34-folded-player-is-largest"] = Layer(
			P("Ann", 100), P("Bob", 100), P("Cal", 500, contesting: false),
			0, null,
			L(700, 100, ["Ann", "Bob"], ("Cal", 500))),
		["35-folds-at-two-levels"] = Layer(
			P("Ann", 30, allIn: true), P("Bob", 80, contesting: false), P("Cal", 150, contesting: false), P("Dee", 150),
			0, null,
			L(120, 30, ["Ann", "Dee"], ("Bob", 30), ("Cal", 30)),
			L(290, 120, ["Dee"], ("Bob", 50), ("Cal", 120))),
		["36-only-one-side-pot-player-remains"] = Layer(
			P("Ann", 50, allIn: true), P("Bob", 120, contesting: false), P("Cal", 120),
			0, null,
			L(150, 50, ["Ann", "Cal"], ("Bob", 50)),
			L(140, 70, ["Cal"], ("Bob", 70))),
		["37-fold-funds-pot-not-eligibility"] = Layer(
			P("Ann", 25), P("Bob", 25), P("Cal", 25, contesting: false),
			0, null,
			L(75, 25, ["Ann", "Bob"], ("Cal", 25))),
		["38-fold-after-another-all-in"] = Layer(
			P("Ann", 60, allIn: true), P("Bob", 140, contesting: false), P("Cal", 140), P("Dee", 140),
			0, null,
			L(240, 60, ["Ann", "Cal", "Dee"], ("Bob", 60)),
			L(240, 80, ["Cal", "Dee"], ("Bob", 80))),
		["52-bet-more-than-opponents-cover"] = Layer(
			P("Ann", 1000, allIn: true), P("Bob", 100, allIn: true), P("Cal", 300, allIn: true),
			700, "Ann",
			L(300, 100, "Ann", "Bob", "Cal"),
			L(400, 200, "Ann", "Cal")),
		["54-top-tier-unmatched"] = Layer(
			P("Ann", 80), P("Bob", 80), P("Cal", 130, allIn: true),
			50, "Cal",
			L(240, 80, "Ann", "Bob", "Cal")),
		["55-higher-bet-still-has-a-matched-part"] = Layer(
			P("Ann", 40, allIn: true), P("Bob", 90, allIn: true), P("Cal", 200),
			110, "Cal",
			L(120, 40, "Ann", "Bob", "Cal"),
			L(100, 50, "Bob", "Cal")),
		["56-lower-tiers-plus-top-excess"] = Layer(
			P("Ann", 15, allIn: true), P("Bob", 40, allIn: true), P("Cal", 40, allIn: true), P("Dee", 75),
			35, "Dee",
			L(60, 15, "Ann", "Bob", "Cal", "Dee"),
			L(75, 25, "Bob", "Cal", "Dee")),
		["57-short-call-others-continue"] = Layer(
			P("Ann", 25, allIn: true), P("Bob", 80), P("Cal", 80),
			0, null,
			L(75, 25, "Ann", "Bob", "Cal"),
			L(110, 55, "Bob", "Cal")),
		["08-fixture-2"] = Layer(
			P("Ann", 50, allIn: true), P("Bob", 100, allIn: true), P("Cal", 200), P("Dee", 200),
			0, null,
			L(200, 50, "Ann", "Bob", "Cal", "Dee"),
			L(150, 50, "Bob", "Cal", "Dee"),
			L(200, 100, "Cal", "Dee")),
		["03-fixture-3-folded-still-funds"] = Layer(
			P("Ann", 100), P("Bob", 100), P("Cal", 100), P("Dee", 100, contesting: false),
			0, null,
			L(400, 100, ["Ann", "Bob", "Cal"], ("Dee", 100)))
	};

	private static BuildCase Layer(
		params object[] parts)
	{
		return Build(parts);
	}

	private static BuildCase Build(object[] parts)
	{
		var players = parts.OfType<PlayerSpec>().ToArray();
		var layers = parts.OfType<LayerSpec>().ToArray();
		var uncalled = parts.OfType<int>().Single();
		var refunded = parts.OfType<string>().SingleOrDefault();
		return new BuildCase(players, uncalled, refunded, layers);
	}

	private static PlayerSpec P(string name, int contribution, bool contesting = true, bool allIn = false) =>
		new(name, contribution, contesting, allIn);

	private static LayerSpec L(int amount, int width, params string[] eligible) =>
		new(amount, width, eligible, []);

	private static LayerSpec L(
		int amount,
		int width,
		string[] eligible,
		params (string Name, int Amount)[] ineligible) =>
		new(amount, width, eligible, ineligible);

	private sealed record PlayerSpec(string Name, int Contribution, bool Contesting, bool AllIn);

	private sealed record LayerSpec(
		int Amount,
		int Width,
		string[] Eligible,
		(string Name, int Amount)[] Ineligible);

	private sealed record BuildCase(
		PlayerSpec[] Players,
		int Uncalled,
		string? Refunded,
		LayerSpec[] Layers);
}
