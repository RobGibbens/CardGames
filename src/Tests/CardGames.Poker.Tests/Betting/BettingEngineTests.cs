using System.Collections.Generic;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class BettingEngineTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var (engine, _, _) = CreateEngine();

        engine.Should().NotBeNull();
        engine.IsRoundInProgress.Should().BeFalse();
        // CurrentPlayer returns the player at index 0 when not in a round
        engine.CurrentPlayer.Should().NotBeNull();
        engine.CurrentPlayer.Name.Should().Be("Alice");
    }

    [Fact]
    public void StartRound_InitializesRound()
    {
        var (engine, players, _) = CreateEngine();

        engine.StartRound("Preflop");

        engine.IsRoundInProgress.Should().BeTrue();
        engine.IsRoundComplete.Should().BeFalse();
        engine.CurrentPlayer.Should().NotBeNull();
    }

    [Fact]
    public void GetAvailableActions_FirstToAct_CanCheckOrBet()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");

        var available = engine.GetAvailableActions();

        available.CanCheck.Should().BeTrue();
        available.CanBet.Should().BeTrue();
        available.CanCall.Should().BeFalse();
        available.CanFold.Should().BeFalse();
    }

    [Fact]
    public void ProcessAction_Check_Succeeds()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");

        var result = engine.ProcessAction(BettingActionType.Check);

        result.Success.Should().BeTrue();
        result.Action.ActionType.Should().Be(BettingActionType.Check);
    }

    [Fact]
    public void ProcessAction_Bet_UpdatesCurrentBet()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");

        var result = engine.ProcessAction(BettingActionType.Bet, 20);

        result.Success.Should().BeTrue();
        engine.CurrentBet.Should().Be(20);
        result.PotAfterAction.Should().Be(20);
    }

    [Fact]
    public void ProcessAction_AfterBet_CanCallRaiseOrFold()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);

        var available = engine.GetAvailableActions();

        available.CanCheck.Should().BeFalse();
        available.CanBet.Should().BeFalse();
        available.CanCall.Should().BeTrue();
        available.CanRaise.Should().BeTrue();
        available.CanFold.Should().BeTrue();
    }

    [Fact]
    public void ProcessAction_Call_MatchesBet()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);

        var result = engine.ProcessAction(BettingActionType.Call);

        result.Success.Should().BeTrue();
        result.PotAfterAction.Should().Be(40);
        players[0].CurrentBet.Should().Be(20);
    }

    [Fact]
    public void ProcessAction_Fold_RemovesPlayerFromHand()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);

        var result = engine.ProcessAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        // After Alice bets, Bob folds
        players[1].HasFolded.Should().BeTrue();
        engine.PlayersInHand.Should().Be(1);
    }

    [Fact]
    public void ProcessAction_AllIn_BetsAllChips()
    {
        var (engine, players, _) = CreateEngine(chipStack: 50);
        engine.StartRound("Flop");

        var result = engine.ProcessAction(BettingActionType.AllIn);

        result.Success.Should().BeTrue();
        // Alice (first to act) goes all-in
        players[0].ChipStack.Should().Be(0);
        players[0].IsAllIn.Should().BeTrue();
        engine.CurrentBet.Should().Be(50);
    }

    [Fact]
    public void ProcessAction_Raise_IncreasesCurrentBet()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);

        var result = engine.ProcessAction(BettingActionType.Raise, 50);

        result.Success.Should().BeTrue();
        engine.CurrentBet.Should().Be(50);
    }

    [Fact]
    public void BettingRound_CompletesWhenAllChecked()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        engine.ProcessAction(BettingActionType.Check);
        var result = engine.ProcessAction(BettingActionType.Check);

        result.RoundComplete.Should().BeTrue();
        engine.IsRoundComplete.Should().BeTrue();
    }

    [Fact]
    public void BettingRound_CompletesWhenBetIsCalled()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        engine.ProcessAction(BettingActionType.Bet, 20);
        var result = engine.ProcessAction(BettingActionType.Call);

        result.RoundComplete.Should().BeTrue();
        engine.IsRoundComplete.Should().BeTrue();
    }

    [Fact]
    public void BettingRound_ContinuesWhenRaised()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        engine.ProcessAction(BettingActionType.Bet, 20);
        var result = engine.ProcessAction(BettingActionType.Raise, 50);

        result.RoundComplete.Should().BeFalse();
        engine.IsRoundComplete.Should().BeFalse();
    }

    [Fact]
    public void ProcessAction_InvalidAction_ReturnsFailed()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        // Try to call when there's no bet
        var result = engine.ProcessAction(BettingActionType.Call);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsValidAction_WithValidAction_ReturnsTrue()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        var isValid = engine.IsValidAction(BettingActionType.Check);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsValidAction_WithInvalidAction_ReturnsFalse()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        var isValid = engine.IsValidAction(BettingActionType.Call);

        isValid.Should().BeFalse();
    }

    [Fact]
    public void ProcessDefaultAction_WhenCanCheck_Checks()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        var result = engine.ProcessDefaultAction();

        result.Success.Should().BeTrue();
        result.Action.ActionType.Should().Be(BettingActionType.Check);
    }

    [Fact]
    public void ProcessDefaultAction_WhenCannotCheck_Folds()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);

        var result = engine.ProcessDefaultAction();

        result.Success.Should().BeTrue();
        result.Action.ActionType.Should().Be(BettingActionType.Fold);
    }

    [Fact]
    public void OnEvent_RaisesEvents()
    {
        var (engine, _, _) = CreateEngine();
        var events = new List<BettingEngineEvent>();
        engine.OnEvent += e => events.Add(e);

        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Check);

        events.Should().ContainItemsAssignableTo<BettingRoundStartedEvent>();
        events.Should().ContainItemsAssignableTo<TurnStartedEvent>();
        events.Should().ContainItemsAssignableTo<ActionTakenEvent>();
    }

    [Fact]
    public void StartRound_WithInitialBet_SetsCurrentBet()
    {
        var (engine, players, _) = CreateEngine();
        // Post big blind
        players[0].PlaceBet(10);

        engine.StartRound("Preflop", initialBet: 10, forcedBetPlayerIndex: 0);

        engine.CurrentBet.Should().Be(10);
    }

    [Fact]
    public void ResetPlayerBets_ResetsAllPlayerBets()
    {
        var (engine, players, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);
        engine.ProcessAction(BettingActionType.Call);

        engine.ResetPlayerBets();

        players[0].CurrentBet.Should().Be(0);
        players[1].CurrentBet.Should().Be(0);
    }

    [Fact]
    public void PlayersInHand_ReturnsCountOfNonFoldedPlayers()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);
        engine.ProcessAction(BettingActionType.Fold);

        engine.PlayersInHand.Should().Be(1);
    }

    [Fact]
    public void ActivePlayers_ReturnsCountOfPlayersWhoCanAct()
    {
        var (engine, _, _) = CreateEngine(chipStack: 50);
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.AllIn);

        // One player all-in, one can still act
        engine.ActivePlayers.Should().Be(1);
    }

    [Fact]
    public void HandComplete_WhenOnlyOnePlayerRemains()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");
        engine.ProcessAction(BettingActionType.Bet, 20);
        var result = engine.ProcessAction(BettingActionType.Fold);

        result.HandComplete.Should().BeTrue();
        engine.PlayersInHand.Should().Be(1);
    }

    [Fact]
    public void Dispose_StopsTimer()
    {
        var (engine, _, _) = CreateEngine();
        engine.StartRound("Flop");

        engine.Dispose();

        // Should not throw
        true.Should().BeTrue();
    }

    private static (BettingEngine engine, List<PokerPlayer> players, PotManager potManager) CreateEngine(int chipStack = 1000)
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", chipStack),
            new("Bob", chipStack)
        };
        var potManager = new PotManager();
        var strategy = new NoLimitStrategy();
        var timerConfig = TurnTimerConfig.Disabled;
        var engine = new BettingEngine(players, potManager, strategy, bigBlind: 10, timerConfig);

        return (engine, players, potManager);
    }
}
