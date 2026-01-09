using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.HoldEm;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class HoldEmGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(Phases.WaitingToStart);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };

        var act = () => new HoldEmGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 11).Select(i => ($"Player{i}", 1000)).ToList();

        var act = () => new HoldEmGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingBlinds()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(Phases.CollectingBlinds);
    }

    [Fact]
    public void CollectBlinds_CollectsFromBothPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectBlinds();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(15); // 5 small blind + 10 big blind
        game.CurrentPhase.Should().Be(Phases.Dealing);
    }

    [Fact]
    public void HeadsUp_DealerIsSmallBlind()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var sbPosition = game.GetSmallBlindPosition();
        var bbPosition = game.GetBigBlindPosition();

        sbPosition.Should().Be(game.DealerPosition);
        bbPosition.Should().Be((game.DealerPosition + 1) % 2);
    }

    [Fact]
    public void ThreePlayers_BlindPositionsCorrect()
    {
        var game = CreateThreePlayerGame();
        game.StartHand();

        var sbPosition = game.GetSmallBlindPosition();
        var bbPosition = game.GetBigBlindPosition();

        sbPosition.Should().Be((game.DealerPosition + 1) % 3);
        bbPosition.Should().Be((game.DealerPosition + 2) % 3);
    }

    [Fact]
    public void DealHoleCards_DealsTwoCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();

        game.DealHoleCards();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(2);
        });
        game.CurrentPhase.Should().Be(Phases.PreFlop);
    }

    [Fact]
    public void StartPreFlopBettingRound_CreatesBettingRound()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();

        game.StartPreFlopBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
    }

    [Fact]
    public void ProcessBettingAction_Call_MatchesBigBlind()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();

        var available = game.GetAvailableActions();
        var result = game.ProcessBettingAction(BettingActionType.Call);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void ProcessBettingAction_Fold_RemovesPlayerFromHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // First player calls
        game.ProcessBettingAction(BettingActionType.Call);
        
        // Big blind raises
        game.ProcessBettingAction(BettingActionType.Raise, 30);

        // First player folds
        var result = game.ProcessBettingAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(Phases.Showdown);
    }

    [Fact]
    public void PreFlopBettingComplete_AdvancesToFlop()
    {
        var game = CreateTwoPlayerGame();
        SetupToFlop(game);

        game.CurrentPhase.Should().Be(Phases.Flop);
    }

    [Fact]
    public void DealFlop_DealsThreeCommunityCards()
    {
        var game = CreateTwoPlayerGame();
        SetupToFlop(game);

        game.DealFlop();

        game.CommunityCards.Should().HaveCount(3);
    }

    [Fact]
    public void FlopBettingComplete_AdvancesToTurn()
    {
        var game = CreateTwoPlayerGame();
        SetupToTurn(game);

        game.CurrentPhase.Should().Be(Phases.Turn);
    }

    [Fact]
    public void DealTurn_DealsFourthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToTurn(game);

        game.DealTurn();

        game.CommunityCards.Should().HaveCount(4);
    }

    [Fact]
    public void TurnBettingComplete_AdvancesToRiver()
    {
        var game = CreateTwoPlayerGame();
        SetupToRiver(game);

        game.CurrentPhase.Should().Be(Phases.River);
    }

    [Fact]
    public void DealRiver_DealsFifthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToRiver(game);

        game.DealRiver();

        game.CommunityCards.Should().HaveCount(5);
    }

    [Fact]
    public void RiverBettingComplete_AdvancesToShowdown()
    {
        var game = CreateTwoPlayerGame();
        SetupToShowdown(game);

        game.CurrentPhase.Should().Be(Phases.Showdown);
    }

    [Fact]
    public void PerformShowdown_ByFold_AwardsPotToLastPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        game.ProcessBettingAction(BettingActionType.Call);
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Fold);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeTrue();
        result.Payouts.Should().HaveCount(1);
        game.CurrentPhase.Should().Be(Phases.Complete);
    }

    [Fact]
    public void PerformShowdown_ComparesHands()
    {
        var game = CreateTwoPlayerGame();
        SetupToShowdown(game);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeFalse();
        result.Payouts.Should().NotBeEmpty();
        result.PlayerHands.Should().HaveCount(2);
    }

    [Fact]
    public void CanContinue_WhenBothHaveChips_ReturnsTrue()
    {
        var game = CreateTwoPlayerGame();

        game.CanContinue().Should().BeTrue();
    }

    [Fact]
    public void CanContinue_WhenOnePlayerHasNoChips_ReturnsFalse()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 0)
        };
        var game = new HoldEmGame(players, smallBlind: 5, bigBlind: 10);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void GetCurrentStreetName_ReturnsCorrectName()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();

        game.GetCurrentStreetName().Should().Be("Pre-Flop");
    }

    [Fact]
    public void GetDealer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var dealer = game.GetDealer();

        dealer.Should().Be(game.GamePlayers[game.DealerPosition]);
    }

    [Fact]
    public void GetSmallBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var sbPlayer = game.GetSmallBlindPlayer();

        sbPlayer.Should().Be(game.GamePlayers[game.GetSmallBlindPosition()]);
    }

    [Fact]
    public void GetBigBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var bbPlayer = game.GetBigBlindPlayer();

        bbPlayer.Should().Be(game.GamePlayers[game.GetBigBlindPosition()]);
    }

    [Fact]
    public void AllIn_CreatesCorrectPotStructure()
    {
        var game = CreateTwoPlayerGameWithDifferentStacks();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Small stack goes all-in
        game.ProcessBettingAction(BettingActionType.AllIn);
        // Big stack calls
        game.ProcessBettingAction(BettingActionType.Call);

        game.TotalPot.Should().BeGreaterThan(0);
    }

    private static HoldEmGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new HoldEmGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static HoldEmGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new HoldEmGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static HoldEmGame CreateTwoPlayerGameWithDifferentStacks()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 500),
            ("Bob", 1000)
        };
        return new HoldEmGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static void SetupToFlop(HoldEmGame game)
    {
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();

        // Complete pre-flop betting
        while (!game.CurrentBettingRound.IsComplete && game.CurrentPhase == Phases.PreFlop)
        {
            var available = game.GetAvailableActions();
            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else if (available.CanCall)
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
        }
    }

    private static void SetupToTurn(HoldEmGame game)
    {
        SetupToFlop(game);

        game.DealFlop();
        game.StartPostFlopBettingRound();

        // Complete flop betting
        while (!game.CurrentBettingRound.IsComplete && game.CurrentPhase == Phases.Flop)
        {
            var available = game.GetAvailableActions();
            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else if (available.CanCall)
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
        }
    }

    private static void SetupToRiver(HoldEmGame game)
    {
        SetupToTurn(game);

        game.DealTurn();
        game.StartPostFlopBettingRound();

        // Complete turn betting
        while (!game.CurrentBettingRound.IsComplete && game.CurrentPhase == Phases.Turn)
        {
            var available = game.GetAvailableActions();
            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else if (available.CanCall)
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
        }
    }

    private static void SetupToShowdown(HoldEmGame game)
    {
        SetupToRiver(game);

        game.DealRiver();
        game.StartPostFlopBettingRound();

        // Complete river betting
        while (!game.CurrentBettingRound.IsComplete && game.CurrentPhase == Phases.River)
        {
            var available = game.GetAvailableActions();
            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else if (available.CanCall)
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
        }
    }
}
