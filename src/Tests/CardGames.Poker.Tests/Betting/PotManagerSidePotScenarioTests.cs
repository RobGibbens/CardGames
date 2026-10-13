using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class PotManagerSidePotScenarioTests
{
	[Fact]
	public void CalculateSidePots_WhenContributionsMatch_KeepsOnePot()
	{
		var manager = Contribute(("Ann", 100, false), ("Bob", 100, false), ("Cal", 100, false));

		manager.Pots.Should().ContainSingle();
		manager.Pots[0].Amount.Should().Be(300);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		manager.TotalPotAmount.Should().Be(300);
	}

	[Fact]
	public void CalculateSidePots_Fixture1_BuildsMain300AndSide200()
	{
		var manager = Contribute(("Ann", 100, true), ("Bob", 200, false), ("Cal", 200, false));

		manager.TotalPotAmount.Should().Be(500);
		manager.Pots.Select(pot => pot.Amount).Should().Equal(300, 200);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		manager.Pots[1].EligiblePlayers.Should().BeEquivalentTo("Bob", "Cal");
	}

	[Fact]
	public void CalculateSidePots_Fixture2_BuildsThreeMatchedTiers()
	{
		var manager = Contribute(
			("Ann", 50, true), ("Bob", 100, true), ("Cal", 200, false), ("Dee", 200, false));

		manager.TotalPotAmount.Should().Be(550);
		manager.Pots.Select(pot => pot.Amount).Should().Equal(200, 150, 200);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal", "Dee");
		manager.Pots[1].EligiblePlayers.Should().BeEquivalentTo("Bob", "Cal", "Dee");
		manager.Pots[2].EligiblePlayers.Should().BeEquivalentTo("Cal", "Dee");
	}

	[Fact]
	public void CalculateSidePots_Fixture3_FoldedPlayerFundsThePotAndIsIneligible()
	{
		var manager = Contribute(
			("Ann", 100, false, false),
			("Bob", 100, false, false),
			("Cal", 100, false, false),
			("Dee", 100, false, true));

		manager.Pots.Should().ContainSingle();
		manager.Pots[0].Amount.Should().Be(400);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		manager.GetPlayerContribution("Dee").Should().Be(100);
	}

	[Fact]
	public void CalculateSidePots_WhenTheTopTierHasOneContestingPlayer_KeepsThatPot()
	{
		// Domain games award this one-player tier. The API refunds it instead.
		var manager = Contribute(("Ann", 20, true), ("Bob", 50, true), ("Cal", 100, true));

		manager.TotalPotAmount.Should().Be(170);
		manager.Pots.Select(pot => pot.Amount).Should().Equal(60, 60, 50);
		manager.Pots[2].EligiblePlayers.Should().BeEquivalentTo("Cal");
	}

	[Fact]
	public void CalculateSidePots_WhenOnlyFoldedPlayersCoverTheTop_RollsThoseChipsIntoTheLastEligiblePot()
	{
		var manager = Contribute(
			("Ann", 50, true, false), ("Bob", 50, true, false), ("Dee", 200, false, true));

		manager.Pots.Should().ContainSingle();
		manager.Pots[0].Amount.Should().Be(300);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob");
		manager.TotalPotAmount.Should().Be(300);
	}

	[Fact]
	public void AwardPots_WhenEachPotHasADifferentBestHand_PaysThoseAmounts()
	{
		var manager = Contribute(("Ann", 100, true), ("Bob", 200, false), ("Cal", 200, false));

		var payouts = manager.AwardPots(eligible =>
			eligible.Contains("Ann") ? ["Ann"] : ["Bob"]);

		payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 300, ["Bob"] = 200 });
		payouts.Values.Sum().Should().Be(500);
	}

	[Fact]
	public void AwardPots_WhenTheMainPotTies_GivesTheOddChipToTheFirstSuppliedWinner()
	{
		var manager = Contribute(("Ann", 50, false), ("Bob", 51, false));

		var payouts = manager.AwardPots(_ => ["Bob", "Ann"]);

		payouts["Bob"].Should().Be(51);
		payouts["Ann"].Should().Be(50);
		payouts.Values.Sum().Should().Be(101);
	}

	[Fact]
	public void AwardPots_WhenTheCallbackRepeatsAWinner_CountsThatPlayerOnce()
	{
		var manager = Contribute(("Ann", 50, false), ("Bob", 51, false));

		var payouts = manager.AwardPots(_ => ["Ann", "Ann", "Bob"]);

		payouts["Ann"].Should().Be(51);
		payouts["Bob"].Should().Be(50);
	}

	[Fact]
	public void AwardPots_WhenTheCallbackNamesAnOutsider_ThrowsInsteadOfPayingThem()
	{
		var manager = Contribute(("Ann", 40, false), ("Bob", 40, false));

		var act = () => manager.AwardPots(_ => ["Eve"]);

		act.Should().Throw<InvalidOperationException>().WithMessage("*no eligible winner*");
		manager.TotalPotAmount.Should().Be(80);
	}

	[Fact]
	public void AwardPots_WhenTheCallbackReturnsNobody_ThrowsInsteadOfDroppingThePot()
	{
		var manager = Contribute(("Ann", 40, false), ("Bob", 40, false));

		var act = () => manager.AwardPots(_ => []);

		act.Should().Throw<InvalidOperationException>().WithMessage("*no eligible winner*");
	}

	[Fact]
	public void AwardPots_WhenAFoldedPlayerIsNamed_DoesNotPayThem()
	{
		var manager = Contribute(
			("Ann", 60, true, false), ("Bob", 60, false, true), ("Cal", 60, false, false));

		var payouts = manager.AwardPots(eligible => eligible.Contains("Ann") ? ["Ann"] : ["Bob"]);

		payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 180 });
	}

	[Fact]
	public void CalculateSidePots_WhenCalledAgain_ReturnsTheSamePots()
	{
		var players = Players(("Ann", 40, true), ("Bob", 90, false), ("Cal", 90, false));
		var manager = new PotManager();
		manager.AddContribution("Ann", 40);
		manager.AddContribution("Bob", 90);
		manager.AddContribution("Cal", 90);
		manager.CalculateSidePots(players);
		var first = Describe(manager);

		manager.CalculateSidePots(players);

		Describe(manager).Should().Be(first);
	}

	[Fact]
	public void CalculateSidePots_WhenPlayerOrderChanges_KeepsAmountsAndEligibleSets()
	{
		var forward = Contribute(("Ann", 25, true), ("Bob", 60, true), ("Cal", 60, false));
		var backward = Contribute(("Cal", 60, false), ("Bob", 60, true), ("Ann", 25, true));

		forward.Pots.Select(pot => pot.Amount).Should().Equal(backward.Pots.Select(pot => pot.Amount));
		for (var index = 0; index < forward.Pots.Count; index++)
			forward.Pots[index].EligiblePlayers.Should().BeEquivalentTo(backward.Pots[index].EligiblePlayers);
	}

	[Fact]
	public void AddContribution_WhenOnePlayerAddsTenMore_AccountsForExactlyTenMoreChips()
	{
		var before = Contribute(("Ann", 40, true), ("Bob", 80, false));
		var after = Contribute(("Ann", 40, true), ("Bob", 90, false));

		after.TotalPotAmount.Should().Be(before.TotalPotAmount + 10);
	}

	[Fact]
	public void Reset_AfterSidePots_DropsContributionsAndLeavesAnEmptyMainPot()
	{
		var manager = Contribute(("Ann", 30, true), ("Bob", 80, false));

		manager.Reset();

		manager.TotalPotAmount.Should().Be(0);
		manager.GetPlayerContribution("Ann").Should().Be(0);
		manager.Pots.Should().ContainSingle();
		manager.Pots[0].Amount.Should().Be(0);
		manager.Pots[0].EligiblePlayers.Should().BeEmpty();
	}

	[Fact]
	public void AddContribution_WhenTheSameNameIsRecordedTwice_KeepsOneRunningTotal()
	{
		var manager = new PotManager();
		manager.AddContribution("Ann", 40);
		manager.AddContribution("Ann", 10);

		manager.GetPlayerContribution("Ann").Should().Be(50);
		manager.TotalPotAmount.Should().Be(50);
		manager.Pots.Should().ContainSingle();
		manager.Pots[0].Amount.Should().Be(50);
		manager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann");
	}

	[Fact]
	public void BettingRound_WhenTheBetExceedsTheStack_RejectsTheAction()
	{
		var players = new List<PokerPlayer> { new("Ann", 40), new("Bob", 100) };
		var round = new BettingRound(players, new PotManager(), dealerPosition: 1, minBet: 10);

		var result = round.ProcessAction(BettingActionType.Bet, 50);

		result.Success.Should().BeFalse();
		result.ErrorMessage.Should().Contain("stack");
		players[0].ChipStack.Should().Be(40);
		round.Actions.Should().BeEmpty();
	}

	[Fact]
	public void AwardPotsSplit_WhenEligiblePlayersTieTheHighHand_GivesTheOddChipInSeatOrder()
	{
		var manager = Contribute(("Ann", 50, false), ("Bob", 51, false));

		var result = manager.AwardPotsSplit(
			_ => [],
			_ => ["Bob", "Ann"],
			["Ann", "Bob"]);

		result.TotalPayouts.Values.Sum().Should().Be(101);
		result.HighHandPayouts["Ann"].Should().Be(51);
		result.HighHandPayouts["Bob"].Should().Be(50);
	}

	private static PotManager Contribute(params (string Name, int Amount, bool AllIn, bool Folded)[] seats)
	{
		var manager = new PotManager();
		foreach (var seat in seats)
			manager.AddContribution(seat.Name, seat.Amount);

		// A fold removes eligibility immediately. CalculateSidePots only rebuilds
		// layers when somebody is all-in, so the fold has to be applied first.
		var players = Players(seats);
		foreach (var player in players.Where(player => player.HasFolded))
			manager.RemovePlayerEligibility(player.Name);

		manager.CalculateSidePots(players);
		return manager;
	}

	private static string Describe(PotManager manager) =>
		string.Join(" | ", manager.Pots.Select(pot =>
			$"{pot.Amount}:{string.Join(",", pot.EligiblePlayers.OrderBy(name => name))}"));

	private static PotManager Contribute(params (string Name, int Amount, bool AllIn)[] seats) =>
		Contribute(seats.Select(seat => (seat.Name, seat.Amount, seat.AllIn, false)).ToArray());

	private static List<PokerPlayer> Players(params (string Name, int Amount, bool AllIn, bool Folded)[] seats)
	{
		var players = new List<PokerPlayer>();
		foreach (var seat in seats)
		{
			var stack = seat.AllIn || seat.Folded ? 0 : 100;
			var player = new PokerPlayer(seat.Name, stack);
			if (seat.Folded)
				player.Fold();
			players.Add(player);
		}

		return players;
	}

	private static List<PokerPlayer> Players(params (string Name, int Amount, bool AllIn)[] seats) =>
		Players(seats.Select(seat => (seat.Name, seat.Amount, seat.AllIn, false)).ToArray());
}
