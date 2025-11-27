using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class OmahaGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(OmahaPhase.WaitingToStart);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };
        
        var act = () => new OmahaGame(players, smallBlind: 5, bigBlind: 10);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 11).Select(i => ($"Player{i}", 1000)).ToList();
        
        var act = () => new OmahaGame(players, smallBlind: 5, bigBlind: 10);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToPostingBlinds()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(OmahaPhase.PostingBlinds);
    }

    [Fact]
    public void PostBlinds_CollectsSmallAndBigBlinds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.PostBlinds();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(15); // 5 small blind + 10 big blind
        game.CurrentPhase.Should().Be(OmahaPhase.Preflop);
    }

    [Fact]
    public void SmallBlindPosition_IsLeftOfDealer()
    {
        var game = CreateTwoPlayerGame(); // Dealer is position 0
        
        game.SmallBlindPosition.Should().Be(1);
    }

    [Fact]
    public void BigBlindPosition_IsLeftOfSmallBlind()
    {
        var game = CreateThreePlayerGame(); // Dealer is position 0
        
        game.BigBlindPosition.Should().Be(2);
    }

    [Fact]
    public void DealHoleCards_DealsFourCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.DealHoleCards();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(4);
        });
    }

    [Fact]
    public void StartBettingRound_CreatesRoundForPreflop()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();

        game.StartBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
        game.GetCurrentMinBet().Should().Be(10); // big blind
    }

    [Fact]
    public void ProcessBettingAction_Call_Succeeds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        var available = game.GetAvailableActions();
        
        if (available.CanCall)
        {
            var result = game.ProcessBettingAction(BettingActionType.Call);
            result.Success.Should().BeTrue();
        }
        else if (available.CanCheck)
        {
            var result = game.ProcessBettingAction(BettingActionType.Check);
            result.Success.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessBettingAction_Fold_RemovesPlayerFromHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        // First player can call or raise
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        
        // Second player folds
        var result = game.ProcessBettingAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(OmahaPhase.Showdown);
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
    public void DealTurn_DealsFourthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToTurn(game);

        game.DealTurn();

        game.CommunityCards.Should().HaveCount(4);
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
    public void PerformShowdown_ByFold_AwardsPotToLastPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Fold);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeTrue();
        result.Payouts.Should().HaveCount(1);
        game.CurrentPhase.Should().Be(OmahaPhase.Complete);
    }

    [Fact]
    public void PerformShowdown_ComparesHands()
    {
        var game = CreateTwoPlayerGame();
        PlayFullHandToShowdown(game);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeFalse();
        result.Payouts.Should().NotBeEmpty();
        result.PlayerHands.Should().HaveCount(2);
    }

    [Fact]
    public void GetCurrentMinBet_ReturnsBigBlind()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.GetCurrentMinBet().Should().Be(10);
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
        var game = new OmahaGame(players, smallBlind: 5, bigBlind: 10);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void GetCurrentStreetName_ReturnsCorrectName()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.GetCurrentStreetName().Should().Be("Preflop");
    }

    [Fact]
    public void DealerMoves_AfterHandComplete()
    {
        var game = CreateTwoPlayerGame();
        var initialDealerPosition = game.DealerPosition;
        
        // Play a hand to completion
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Fold);
        game.PerformShowdown();

        game.DealerPosition.Should().NotBe(initialDealerPosition);
    }

    [Fact]
    public void GetDealer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();
        
        var dealer = game.GetDealer();
        
        dealer.Should().NotBeNull();
        dealer.Player.Name.Should().Be("Alice");
    }

    [Fact]
    public void GetSmallBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();
        
        var sbPlayer = game.GetSmallBlindPlayer();
        
        sbPlayer.Should().NotBeNull();
        sbPlayer.Player.Name.Should().Be("Bob"); // Left of dealer
    }

    [Fact]
    public void GetBigBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateThreePlayerGame();
        
        var bbPlayer = game.GetBigBlindPlayer();
        
        bbPlayer.Should().NotBeNull();
        bbPlayer.Player.Name.Should().Be("Charlie"); // Left of small blind
    }

    private static OmahaGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new OmahaGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static OmahaGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new OmahaGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static void SetupToFlop(OmahaGame game)
    {
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        
        // Complete preflop betting (everyone calls/checks)
        while (!game.CurrentBettingRound.IsComplete)
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

    private static void SetupToTurn(OmahaGame game)
    {
        SetupToFlop(game);
        
        // Flop
        game.DealFlop();
        game.StartBettingRound();
        while (!game.CurrentBettingRound.IsComplete)
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

    private static void SetupToRiver(OmahaGame game)
    {
        SetupToTurn(game);
        
        // Turn
        game.DealTurn();
        game.StartBettingRound();
        while (!game.CurrentBettingRound.IsComplete)
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

    private static void PlayFullHandToShowdown(OmahaGame game)
    {
        SetupToRiver(game);
        
        // River
        game.DealRiver();
        game.StartBettingRound();
        while (!game.CurrentBettingRound.IsComplete)
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
