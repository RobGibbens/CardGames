using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.HoldEm;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class HoldEmSidePotFlowTests
{
	[Fact]
	public void PreflopAllIn_ThenFlopBet_BuildsTheSidePotFromBothStreets()
	{
		var game = new HoldEmGame([("Ann", 400), ("Bob", 400), ("Cal", 80)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();

		Act(game, BettingActionType.Raise, 80);
		Act(game, BettingActionType.Call);
		Act(game, BettingActionType.Call);
		game.CurrentPhase.Should().Be(Phases.Flop);
		game.PotManager.GetPlayerContribution("Ann").Should().Be(80);
		game.PotManager.GetPlayerContribution("Bob").Should().Be(80);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(80);

		game.DealFlop();
		game.StartPostFlopBettingRound();
		Act(game, BettingActionType.Bet, 100);
		Act(game, BettingActionType.Call);

		game.PotManager.GetPlayerContribution("Ann").Should().Be(180);
		game.PotManager.GetPlayerContribution("Bob").Should().Be(180);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(80);
		game.PotManager.TotalPotAmount.Should().Be(440);
		game.PotManager.Pots.Select(pot => pot.Amount).Should().Equal(240, 200);
		game.PotManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		game.PotManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob");
	}

	[Fact]
	public void CheckedToTheRiver_AllInCall_UsesTheWholeHandNotJustTheRiver()
	{
		var game = new HoldEmGame([("Ann", 500), ("Bob", 200), ("Cal", 500)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();
		Act(game, BettingActionType.Call);
		Act(game, BettingActionType.Call);
		game.CurrentPhase.Should().Be(Phases.Flop);

		PlayCheckedStreet(game, Phases.Flop, game.DealFlop);
		PlayCheckedStreet(game, Phases.Turn, game.DealTurn);
		game.CurrentPhase.Should().Be(Phases.River);
		game.DealRiver();
		game.StartPostFlopBettingRound();

		// Bob is first and short. He shoves. Ann reshoves. Cal calls Ann.
		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.Call);

		game.PotManager.GetPlayerContribution("Ann").Should().Be(500);
		game.PotManager.GetPlayerContribution("Bob").Should().Be(200);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(500);
		game.PotManager.TotalPotAmount.Should().Be(1_200);
		game.PotManager.Pots.Select(pot => pot.Amount).Should().Equal(600, 600);
		game.PotManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		game.PotManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo("Ann", "Cal");
	}

	[Fact]
	public void AllInOnTheFlop_BuildsTheSidePotFromPreflopAndFlop()
	{
		var game = new HoldEmGame([("Ann", 500), ("Bob", 200), ("Cal", 500)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();
		Act(game, BettingActionType.Call);
		Act(game, BettingActionType.Call);

		game.CurrentPhase.Should().Be(Phases.Flop);
		game.DealFlop();
		game.CommunityCards.Should().HaveCount(3);
		game.StartPostFlopBettingRound();
		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.Call);

		game.PotManager.GetPlayerContribution("Bob").Should().Be(200);
		game.PotManager.GetPlayerContribution("Ann").Should().Be(500);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(500);
		game.PotManager.Pots.Select(pot => pot.Amount).Should().Equal(600, 600);
		game.PotManager.TotalPotAmount.Should().Be(1_200);
	}

	[Fact]
	public void AllInOnTheTurn_UsesPreflopAndTurnContributions()
	{
		var game = new HoldEmGame([("Ann", 500), ("Bob", 200), ("Cal", 500)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();
		Act(game, BettingActionType.Call);
		Act(game, BettingActionType.Call);

		PlayCheckedStreet(game, Phases.Flop, game.DealFlop);
		game.CurrentPhase.Should().Be(Phases.Turn);
		game.DealTurn();
		game.CommunityCards.Should().HaveCount(4);
		game.StartPostFlopBettingRound();

		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.AllIn);
		Act(game, BettingActionType.Call);

		game.PotManager.GetPlayerContribution("Ann").Should().Be(500);
		game.PotManager.GetPlayerContribution("Bob").Should().Be(200);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(500);
		game.PotManager.Pots.Select(pot => pot.Amount).Should().Equal(600, 600);
		game.PotManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo("Ann", "Bob", "Cal");
		game.PotManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo("Ann", "Cal");
		game.PotManager.TotalPotAmount.Should().Be(1_200);
	}

	[Fact]
	public void EveryoneFoldsToTheRaiser_AwardsTheBlindsWithoutAShowdown()
	{
		var game = new HoldEmGame([("Ann", 400), ("Bob", 400), ("Cal", 400)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();
		Act(game, BettingActionType.Raise, 60);
		Act(game, BettingActionType.Fold);
		Act(game, BettingActionType.Fold);

		var result = game.PerformShowdown();

		result.Success.Should().BeTrue();
		result.WonByFold.Should().BeTrue();
		result.Payouts.Should().BeEquivalentTo(new System.Collections.Generic.Dictionary<string, int> { ["Ann"] = 90 });
		game.Players.Single(player => player.Name == "Ann").ChipStack.Should().Be(430);
		game.Players.Single(player => player.Name == "Bob").ChipStack.Should().Be(390);
		game.Players.Single(player => player.Name == "Cal").ChipStack.Should().Be(380);
		game.Players.Sum(player => player.ChipStack).Should().Be(1200);
	}

	[Fact]
	public void NextHand_ClearsThePreviousSidePots()
	{
		var game = new HoldEmGame([("Ann", 400), ("Bob", 400), ("Cal", 80)], smallBlind: 10, bigBlind: 20);
		game.StartHand();
		game.CollectBlinds();
		game.DealHoleCards();
		game.StartPreFlopBettingRound();
		Act(game, BettingActionType.Raise, 80);
		Act(game, BettingActionType.Fold);
		Act(game, BettingActionType.Fold);
		game.PerformShowdown();

		game.StartHand();

		game.PotManager.TotalPotAmount.Should().Be(0);
		game.PotManager.Pots.Should().ContainSingle();
		game.PotManager.GetPlayerContribution("Ann").Should().Be(0);
		game.PotManager.GetPlayerContribution("Bob").Should().Be(0);
		game.PotManager.GetPlayerContribution("Cal").Should().Be(0);
		game.Players.Single(player => player.Name == "Ann").ChipStack.Should().Be(430);
		game.Players.Single(player => player.Name == "Bob").ChipStack.Should().Be(390);
		game.Players.Single(player => player.Name == "Cal").ChipStack.Should().Be(60);
		game.Players.Sum(player => player.ChipStack).Should().Be(880);
	}

	private static void PlayCheckedStreet(HoldEmGame game, Phases phase, System.Action deal)
	{
		game.CurrentPhase.Should().Be(phase);
		deal();
		game.StartPostFlopBettingRound();
		while (game.CurrentPhase == phase && !game.CurrentBettingRound.IsComplete)
		{
			var available = game.GetAvailableActions();
			if (available.CanCheck)
				Act(game, BettingActionType.Check);
			else
				Act(game, BettingActionType.Call);
		}
	}

	private static BettingRoundResult Act(HoldEmGame game, BettingActionType action, int amount = 0)
	{
		var result = game.ProcessBettingAction(action, amount);
		result.Success.Should().BeTrue(result.ErrorMessage);
		return result;
	}
}
