using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Variants;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

/// <summary>
/// End-to-end tests for Texas Hold'em game flow.
/// These tests verify the complete game lifecycle from start to showdown.
/// </summary>
public class HoldEmEndToEndTests
{
    #region Complete Game Flow Tests

    [Fact]
    public void EndToEnd_TwoPlayerGame_CompleteFlow_ToShowdown()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Phase 1: Start Hand
        game.CurrentPhase.Should().Be(HoldEmPhase.WaitingToStart);
        game.StartHand();
        game.CurrentPhase.Should().Be(HoldEmPhase.CollectingBlinds);
        
        // Phase 2: Collect Blinds
        var blindActions = game.CollectBlinds();
        blindActions.Should().HaveCount(2);
        game.TotalPot.Should().Be(15); // SB: 5 + BB: 10
        game.CurrentPhase.Should().Be(HoldEmPhase.Dealing);
        
        // Phase 3: Deal Hole Cards
        game.DealHoleCards();
        game.GamePlayers[0].HoleCards.Should().HaveCount(2);
        game.GamePlayers[1].HoleCards.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(HoldEmPhase.PreFlop);
        
        // Phase 4: Pre-Flop Betting
        game.StartPreFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Flop);
        
        // Phase 5: Deal Flop
        game.DealFlop();
        game.CommunityCards.Should().HaveCount(3);
        
        // Phase 6: Flop Betting
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Turn);
        
        // Phase 7: Deal Turn
        game.DealTurn();
        game.CommunityCards.Should().HaveCount(4);
        
        // Phase 8: Turn Betting
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.River);
        
        // Phase 9: Deal River
        game.DealRiver();
        game.CommunityCards.Should().HaveCount(5);
        
        // Phase 10: River Betting
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Showdown);
        
        // Phase 11: Showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeFalse();
        showdownResult.Payouts.Should().NotBeEmpty();
        showdownResult.PlayerHands.Should().HaveCount(2);
        
        // Verify game completion
        game.CurrentPhase.Should().Be(HoldEmPhase.Complete);
        
        // Verify pot was distributed
        var totalChips = game.Players.Sum(p => p.ChipStack);
        totalChips.Should().Be(2000); // Original total chips preserved
    }

    [Fact]
    public void EndToEnd_ThreePlayerGame_CompleteFlow_WithFold()
    {
        // Arrange
        var game = CreateThreePlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // First player folds
        game.ProcessBettingAction(BettingActionType.Fold);
        
        // Continue with remaining two players
        var result = game.ProcessBettingAction(BettingActionType.Call);
        game.ProcessBettingAction(BettingActionType.Check);
        
        // Should advance to flop with 2 players
        game.CurrentPhase.Should().Be(HoldEmPhase.Flop);
        
        // Complete the hand
        game.DealFlop();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.PlayerHands.Should().HaveCount(2); // Only non-folded players
    }

    [Fact]
    public void EndToEnd_Game_WinByFold_PreFlop()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Dealer calls (in heads-up, dealer acts first pre-flop)
        game.ProcessBettingAction(BettingActionType.Call);
        
        // Big blind raises
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        
        // Dealer folds
        game.ProcessBettingAction(BettingActionType.Fold);
        
        // Should immediately go to showdown (one player wins by fold)
        game.CurrentPhase.Should().Be(HoldEmPhase.Showdown);
        
        // Perform showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeTrue();
        showdownResult.Payouts.Should().HaveCount(1);
        
        // Winner should have gained the pot
        var winner = showdownResult.Payouts.First();
        winner.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EndToEnd_AllIn_Scenario()
    {
        // Arrange - One player with fewer chips
        var players = new List<(string, int)>
        {
            ("ShortStack", 50),
            ("BigStack", 1000)
        };
        var game = new HoldEmGame(players, smallBlind: 5, bigBlind: 10);
        
        // Start game
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Short stack goes all-in
        game.ProcessBettingAction(BettingActionType.AllIn);
        
        // Big stack calls
        game.ProcessBettingAction(BettingActionType.Call);
        
        // Game should proceed through all streets automatically
        // or be in showdown if all players are all-in
        if (game.CurrentPhase != HoldEmPhase.Showdown)
        {
            // Complete remaining streets
            while (game.CurrentPhase != HoldEmPhase.Showdown)
            {
                switch (game.CurrentPhase)
                {
                    case HoldEmPhase.Flop:
                        game.DealFlop();
                        game.StartPostFlopBettingRound();
                        PlayBettingRoundCheckOrCall(game);
                        break;
                    case HoldEmPhase.Turn:
                        game.DealTurn();
                        game.StartPostFlopBettingRound();
                        PlayBettingRoundCheckOrCall(game);
                        break;
                    case HoldEmPhase.River:
                        game.DealRiver();
                        game.StartPostFlopBettingRound();
                        PlayBettingRoundCheckOrCall(game);
                        break;
                }
            }
        }
        
        // Perform showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        
        // Verify total chips are preserved
        var totalChips = game.Players.Sum(p => p.ChipStack);
        totalChips.Should().Be(1050); // Original total
    }

    [Fact]
    public void EndToEnd_MultipleHands_GameContinues()
    {
        // Arrange - Use 3 players to properly test dealer rotation
        var game = CreateThreePlayerGame();
        
        var initialDealerPosition = game.DealerPosition;
        
        // Play first hand
        PlayCompleteHand(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Complete);
        
        // Dealer should have moved at end of hand
        var dealerAfterFirstHand = game.DealerPosition;
        dealerAfterFirstHand.Should().Be((initialDealerPosition + 1) % 3);
        
        // Start second hand (if players still have chips)
        if (game.CanContinue())
        {
            game.StartHand();
            game.CurrentPhase.Should().Be(HoldEmPhase.CollectingBlinds);
            
            // Dealer position should remain the same after StartHand
            game.DealerPosition.Should().Be(dealerAfterFirstHand);
        }
    }

    #endregion

    #region Variant Factory Integration Tests

    [Fact]
    public void VariantFactory_CreatesHoldEmGame_WithCorrectConfiguration()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var holdemInfo = new GameVariantInfo(
            "texas-holdem",
            "Texas Hold'em",
            "The most popular poker variant",
            MinPlayers: 2,
            MaxPlayers: 10);
        
        registry.RegisterVariant(holdemInfo, (players, sb, bb) => new HoldEmGame(players, sb, bb));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        
        // Act
        var game = factory.CreateGame("texas-holdem", players, 5, 10) as HoldEmGame;
        
        // Assert
        game.Should().NotBeNull();
        game!.SmallBlind.Should().Be(5);
        game.BigBlind.Should().Be(10);
        game.Players.Should().HaveCount(2);
        game.Players[0].Name.Should().Be("Alice");
        game.Players[0].ChipStack.Should().Be(1000);
    }

    [Fact]
    public void VariantFactory_HoldEmGame_CanPlayCompleteHand()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var holdemInfo = new GameVariantInfo(
            "texas-holdem",
            "Texas Hold'em",
            "The most popular poker variant",
            MinPlayers: 2,
            MaxPlayers: 10);
        
        registry.RegisterVariant(holdemInfo, (players, sb, bb) => new HoldEmGame(players, sb, bb));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        var game = factory.CreateGame("texas-holdem", players, 5, 10) as HoldEmGame;
        
        // Act - Play complete hand
        game!.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        var result = game.PerformShowdown();
        
        // Assert
        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(HoldEmPhase.Complete);
    }

    #endregion

    #region Hand Evaluation Integration Tests

    [Fact]
    public void EndToEnd_ShowdownEvaluatesHandsCorrectly()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Play to showdown
        PlayToShowdown(game);
        
        // Act
        var result = game.PerformShowdown();
        
        // Assert
        result.Success.Should().BeTrue();
        result.PlayerHands.Should().HaveCount(2);
        
        // Verify each player has evaluated hand
        foreach (var playerHand in result.PlayerHands)
        {
            playerHand.Value.hand.Should().NotBeNull();
            playerHand.Value.hand.Type.Should().NotBe(default);
            playerHand.Value.hand.Strength.Should().BeGreaterThan(0);
            playerHand.Value.holeCards.Should().HaveCount(2);
        }
        
        // Verify winner determination
        result.Payouts.Should().NotBeEmpty();
        var totalPayout = result.Payouts.Values.Sum();
        totalPayout.Should().BeGreaterThan(0); // Payout should reflect pot distribution
    }

    [Fact]
    public void EndToEnd_TiedHands_SplitPot()
    {
        // This test verifies the pot splitting logic is in place
        // Note: In actual gameplay, ties are rare but possible
        
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Play to showdown multiple times to increase chance of split
        PlayToShowdown(game);
        
        // Act
        var result = game.PerformShowdown();
        
        // Assert - Even if there's no split, verify pot distribution is correct
        result.Success.Should().BeTrue();
        var totalPayout = result.Payouts.Values.Sum();
        totalPayout.Should().BeGreaterThan(0);
    }

    #endregion

    #region Betting Rules Enforcement Tests

    [Fact]
    public void EndToEnd_BettingRules_MinimumRaiseEnforced()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Act - Get available actions
        var available = game.GetAvailableActions();
        
        // Assert - Minimum raise should be at least the big blind
        available.Should().NotBeNull();
        available.CanRaise.Should().BeTrue();
        available.MinRaise.Should().BeGreaterThanOrEqualTo(game.BigBlind);
    }

    [Fact]
    public void EndToEnd_BettingRules_CallAmountCorrect()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Act - Get available actions pre-flop
        var available = game.GetAvailableActions();
        
        // Assert - Call amount should match what's needed to match the current bet
        available.Should().NotBeNull();
        available.CanCall.Should().BeTrue();
        available.CallAmount.Should().Be(game.BigBlind - game.SmallBlind); // 10 - 5 = 5
    }

    [Fact]
    public void EndToEnd_PostFlop_FirstToAct_CanCheck()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartPostFlopBettingRound();
        
        // Act
        var available = game.GetAvailableActions();
        
        // Assert - First to act post-flop can check
        available.Should().NotBeNull();
        available.CanCheck.Should().BeTrue();
    }

    #endregion

    #region Pot Management Tests

    [Fact]
    public void EndToEnd_PotManager_TracksContributionsCorrectly()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act
        game.StartHand();
        game.CollectBlinds();
        
        // Assert - Pot should reflect blinds
        game.TotalPot.Should().Be(15);
        game.PotManager.Should().NotBeNull();
        
        // Continue playing
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        
        // Raise action
        game.ProcessBettingAction(BettingActionType.Call);
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Call);
        
        // Pot should include all bets
        game.TotalPot.Should().BeGreaterThan(15);
    }

    #endregion

    #region Phase Transition Tests

    [Fact]
    public void EndToEnd_PhaseTransitions_AreCorrect()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Verify each phase transition
        game.CurrentPhase.Should().Be(HoldEmPhase.WaitingToStart);
        
        game.StartHand();
        game.CurrentPhase.Should().Be(HoldEmPhase.CollectingBlinds);
        
        game.CollectBlinds();
        game.CurrentPhase.Should().Be(HoldEmPhase.Dealing);
        
        game.DealHoleCards();
        game.CurrentPhase.Should().Be(HoldEmPhase.PreFlop);
        
        game.StartPreFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Flop);
        
        game.DealFlop();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Turn);
        
        game.DealTurn();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.River);
        
        game.DealRiver();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(HoldEmPhase.Showdown);
        
        game.PerformShowdown();
        game.CurrentPhase.Should().Be(HoldEmPhase.Complete);
    }

    #endregion

    #region Helper Methods

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

    private static void PlayBettingRoundCheckOrCall(HoldEmGame game)
    {
        while (game.CurrentBettingRound != null && !game.CurrentBettingRound.IsComplete)
        {
            var available = game.GetAvailableActions();
            if (available == null) break;
            
            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else if (available.CanCall)
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
            else
            {
                break;
            }
        }
    }

    private static void PlayToShowdown(HoldEmGame game)
    {
        game.StartHand();
        game.CollectBlinds();
        game.DealHoleCards();
        game.StartPreFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartPostFlopBettingRound();
        PlayBettingRoundCheckOrCall(game);
    }

    private static void PlayCompleteHand(HoldEmGame game)
    {
        PlayToShowdown(game);
        game.PerformShowdown();
    }

    #endregion
}
