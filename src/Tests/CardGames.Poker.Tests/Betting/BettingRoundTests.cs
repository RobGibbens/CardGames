using System.Collections.Generic;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class BettingRoundTests
{
    [Fact]
    public void GetAvailableActions_FirstToAct_CanCheckOrBet()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        var available = round.GetAvailableActions();

        available.CanCheck.Should().BeTrue();
        available.CanBet.Should().BeTrue();
        available.CanCall.Should().BeFalse();
        available.CanFold.Should().BeFalse(); // Can't fold when you can check
    }

    [Fact]
    public void ProcessAction_Check_MovesToNextPlayer()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        var result = round.ProcessAction(BettingActionType.Check);

        result.Success.Should().BeTrue();
        result.Action.ActionType.Should().Be(BettingActionType.Check);
        round.CurrentPlayer.Name.Should().Be("Alice"); // Wraps around to Alice
    }

    [Fact]
    public void ProcessAction_Bet_SetsCurrentBet()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        var result = round.ProcessAction(BettingActionType.Bet, 20);

        result.Success.Should().BeTrue();
        round.CurrentBet.Should().Be(20);
        potManager.TotalPotAmount.Should().Be(20);
    }

    [Fact]
    public void GetAvailableActions_AfterBet_CanCallRaiseOrFold()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);
        round.ProcessAction(BettingActionType.Bet, 20);

        var available = round.GetAvailableActions();

        available.CanCheck.Should().BeFalse();
        available.CanBet.Should().BeFalse();
        available.CanCall.Should().BeTrue();
        available.CanRaise.Should().BeTrue();
        available.CanFold.Should().BeTrue();
        available.CallAmount.Should().Be(20);
    }

    [Fact]
    public void ProcessAction_Call_MatchesBet()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);
        round.ProcessAction(BettingActionType.Bet, 20);

        var result = round.ProcessAction(BettingActionType.Call);

        result.Success.Should().BeTrue();
        players[0].CurrentBet.Should().Be(20);
        potManager.TotalPotAmount.Should().Be(40);
    }

    [Fact]
    public void ProcessAction_Fold_RemovesPlayerFromHand()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);
        round.ProcessAction(BettingActionType.Bet, 20);

        var result = round.ProcessAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        players[0].HasFolded.Should().BeTrue();
        round.PlayersInHand.Should().Be(1);
    }

    [Fact]
    public void ProcessAction_AllIn_BetsAllChips()
    {
        var players = CreateTwoPlayers(chipStack: 50);
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        var result = round.ProcessAction(BettingActionType.AllIn);

        result.Success.Should().BeTrue();
        players[1].ChipStack.Should().Be(0);
        players[1].IsAllIn.Should().BeTrue();
        round.CurrentBet.Should().Be(50);
    }

    [Fact]
    public void BettingRound_CompletesWhenAllChecked()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        round.ProcessAction(BettingActionType.Check);
        var result = round.ProcessAction(BettingActionType.Check);

        result.RoundComplete.Should().BeTrue();
        round.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void BettingRound_CompletesWhenBetIsCalled()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        round.ProcessAction(BettingActionType.Bet, 20);
        var result = round.ProcessAction(BettingActionType.Call);

        result.RoundComplete.Should().BeTrue();
        round.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void BettingRound_ContinuesWhenRaised()
    {
        var players = CreateTwoPlayers();
        var potManager = new PotManager();
        var round = new BettingRound(players, potManager, dealerPosition: 0, minBet: 10);

        round.ProcessAction(BettingActionType.Bet, 20);
        var result = round.ProcessAction(BettingActionType.Raise, 40);

        result.RoundComplete.Should().BeFalse();
        round.IsComplete.Should().BeFalse();
    }

    private static List<PokerPlayer> CreateTwoPlayers(int chipStack = 5000)
    {
        return
        [
            new PokerPlayer("Alice", chipStack),
            new PokerPlayer("Bob", chipStack)
        ];
    }
}
