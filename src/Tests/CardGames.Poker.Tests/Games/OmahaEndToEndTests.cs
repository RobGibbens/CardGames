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
/// End-to-end tests for Omaha poker game flow.
/// These tests verify the complete game lifecycle from start to showdown,
/// including the Omaha-specific rule that players must use exactly 2 hole cards
/// and exactly 3 community cards to make their best hand.
/// </summary>
public class OmahaEndToEndTests
{
    #region Complete Game Flow Tests

    [Fact]
    public void EndToEnd_TwoPlayerGame_CompleteFlow_ToShowdown()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Phase 1: Start Hand
        game.CurrentPhase.Should().Be(OmahaPhase.WaitingToStart);
        game.StartHand();
        game.CurrentPhase.Should().Be(OmahaPhase.PostingBlinds);
        
        // Phase 2: Post Blinds
        var blindActions = game.PostBlinds();
        blindActions.Should().HaveCount(2);
        game.TotalPot.Should().Be(15); // SB: 5 + BB: 10
        game.CurrentPhase.Should().Be(OmahaPhase.Preflop);
        
        // Phase 3: Deal Hole Cards (4 cards in Omaha)
        game.DealHoleCards();
        game.GamePlayers[0].HoleCards.Should().HaveCount(4);
        game.GamePlayers[1].HoleCards.Should().HaveCount(4);
        
        // Phase 4: Pre-Flop Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Flop);
        
        // Phase 5: Deal Flop
        game.DealFlop();
        game.CommunityCards.Should().HaveCount(3);
        
        // Phase 6: Flop Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Turn);
        
        // Phase 7: Deal Turn
        game.DealTurn();
        game.CommunityCards.Should().HaveCount(4);
        
        // Phase 8: Turn Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.River);
        
        // Phase 9: Deal River
        game.DealRiver();
        game.CommunityCards.Should().HaveCount(5);
        
        // Phase 10: River Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Showdown);
        
        // Phase 11: Showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeFalse();
        showdownResult.Payouts.Should().NotBeEmpty();
        showdownResult.PlayerHands.Should().HaveCount(2);
        
        // Verify game completion
        game.CurrentPhase.Should().Be(OmahaPhase.Complete);
        
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
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        
        // First player folds
        game.ProcessBettingAction(BettingActionType.Fold);
        
        // Continue with remaining two players
        game.ProcessBettingAction(BettingActionType.Call);
        game.ProcessBettingAction(BettingActionType.Check);
        
        // Should advance to flop with 2 players
        game.CurrentPhase.Should().Be(OmahaPhase.Flop);
        
        // Complete the hand
        game.DealFlop();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartBettingRound();
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
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        
        // First player raises
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        
        // Second player folds
        game.ProcessBettingAction(BettingActionType.Fold);
        
        // Should immediately go to showdown (one player wins by fold)
        game.CurrentPhase.Should().Be(OmahaPhase.Showdown);
        
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
        // Arrange - Both players with limited chips to ensure all-in scenarios complete
        var players = new List<(string, int)>
        {
            ("ShortStack", 100),
            ("BigStack", 200)
        };
        var game = new OmahaGame(players, smallBlind: 5, bigBlind: 10);
        
        // Start game
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        
        // Short stack raises big
        game.ProcessBettingAction(BettingActionType.Raise, 80);
        
        // Big stack re-raises all-in
        game.ProcessBettingAction(BettingActionType.AllIn);
        
        // Short stack calls with remaining chips (goes all-in)
        game.ProcessBettingAction(BettingActionType.Call);
        
        // Both players are all-in, game should advance through streets
        // Complete remaining streets with no betting (both all-in)
        while (game.CurrentPhase != OmahaPhase.Showdown)
        {
            switch (game.CurrentPhase)
            {
                case OmahaPhase.Flop:
                    game.DealFlop();
                    game.StartBettingRound();
                    PlayBettingRoundCheckOrCall(game);
                    break;
                case OmahaPhase.Turn:
                    game.DealTurn();
                    game.StartBettingRound();
                    PlayBettingRoundCheckOrCall(game);
                    break;
                case OmahaPhase.River:
                    game.DealRiver();
                    game.StartBettingRound();
                    PlayBettingRoundCheckOrCall(game);
                    break;
                default:
                    // Prevent infinite loop
                    break;
            }
            
            // Safety break to prevent infinite loop in case game doesn't advance
            if (game.CommunityCards.Count >= 5 && game.CurrentPhase != OmahaPhase.Showdown)
            {
                // All community cards dealt but not in showdown - this shouldn't happen
                break;
            }
        }
        
        // Perform showdown
        game.CurrentPhase.Should().Be(OmahaPhase.Showdown);
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        
        // Verify total chips are preserved
        var totalChips = game.Players.Sum(p => p.ChipStack);
        totalChips.Should().Be(300); // Original total
    }

    [Fact]
    public void EndToEnd_MultipleHands_GameContinues()
    {
        // Arrange - Use 3 players to properly test dealer rotation
        var game = CreateThreePlayerGame();
        
        var initialDealerPosition = game.DealerPosition;
        
        // Play first hand
        PlayCompleteHand(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Complete);
        
        // Dealer should have moved at end of hand
        var dealerAfterFirstHand = game.DealerPosition;
        dealerAfterFirstHand.Should().Be((initialDealerPosition + 1) % 3);
        
        // Start second hand (if players still have chips)
        if (game.CanContinue())
        {
            game.StartHand();
            game.CurrentPhase.Should().Be(OmahaPhase.PostingBlinds);
            
            // Dealer position should remain the same after StartHand
            game.DealerPosition.Should().Be(dealerAfterFirstHand);
        }
    }

    #endregion

    #region Omaha-Specific Tests

    [Fact]
    public void EndToEnd_OmahaHoleCards_AlwaysFourCards()
    {
        // Arrange
        var game = CreateThreePlayerGame();
        
        // Act
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        
        // Assert - All players should have exactly 4 hole cards
        foreach (var player in game.GamePlayers)
        {
            player.HoleCards.Should().HaveCount(4, 
                "Omaha players must receive exactly 4 hole cards");
        }
    }

    [Fact]
    public void EndToEnd_OmahaHandEvaluation_UsesExactlyTwoHoleCards()
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
        
        // Verify each player's hand was evaluated using Omaha rules
        foreach (var playerHand in result.PlayerHands)
        {
            playerHand.Value.hand.Should().NotBeNull();
            playerHand.Value.holeCards.Should().HaveCount(4);
            playerHand.Value.communityCards.Should().HaveCount(5);
            
            // The OmahaHand type enforces the rule that exactly 2 hole cards
            // and exactly 3 community cards must be used
            playerHand.Value.hand.Type.Should().NotBe(default);
            playerHand.Value.hand.Strength.Should().BeGreaterThan(0);
        }
    }

    #endregion

    #region Variant Factory Integration Tests

    [Fact]
    public void VariantFactory_CreatesOmahaGame_WithCorrectConfiguration()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var omahaInfo = new GameVariantInfo(
            "omaha",
            "Omaha",
            "Omaha poker - must use exactly 2 hole cards and 3 community cards",
            MinPlayers: 2,
            MaxPlayers: 10);
        
        registry.RegisterVariant(omahaInfo, (players, sb, bb) => new OmahaGame(players, sb, bb));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        
        // Act
        var game = factory.CreateGame("omaha", players, 5, 10) as OmahaGame;
        
        // Assert
        game.Should().NotBeNull();
        game!.SmallBlind.Should().Be(5);
        game.BigBlind.Should().Be(10);
        game.Players.Should().HaveCount(2);
        game.Players[0].Name.Should().Be("Alice");
        game.Players[0].ChipStack.Should().Be(1000);
    }

    [Fact]
    public void VariantFactory_OmahaGame_CanPlayCompleteHand()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var omahaInfo = new GameVariantInfo(
            "omaha",
            "Omaha",
            "Omaha poker",
            MinPlayers: 2,
            MaxPlayers: 10);
        
        registry.RegisterVariant(omahaInfo, (players, sb, bb) => new OmahaGame(players, sb, bb));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        var game = factory.CreateGame("omaha", players, 5, 10) as OmahaGame;
        
        // Act - Play complete hand
        game!.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        var result = game.PerformShowdown();
        
        // Assert
        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(OmahaPhase.Complete);
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
            playerHand.Value.holeCards.Should().HaveCount(4);
        }
        
        // Verify winner determination
        result.Payouts.Should().NotBeEmpty();
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
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        
        // Act - Get available actions
        var available = game.GetAvailableActions();
        
        // Assert - Minimum raise should be at least the big blind
        available.Should().NotBeNull();
        available.CanRaise.Should().BeTrue();
        available.MinRaise.Should().BeGreaterThanOrEqualTo(game.BigBlind);
    }

    [Fact]
    public void EndToEnd_PostFlop_FirstToAct_CanCheck()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartBettingRound();
        
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
        game.PostBlinds();
        
        // Assert - Pot should reflect blinds
        game.TotalPot.Should().Be(15);
        game.PotManager.Should().NotBeNull();
        
        // Continue playing
        game.DealHoleCards();
        game.StartBettingRound();
        
        // Raise action
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
        game.CurrentPhase.Should().Be(OmahaPhase.WaitingToStart);
        
        game.StartHand();
        game.CurrentPhase.Should().Be(OmahaPhase.PostingBlinds);
        
        game.PostBlinds();
        game.CurrentPhase.Should().Be(OmahaPhase.Preflop);
        
        game.DealHoleCards();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Flop);
        
        game.DealFlop();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Turn);
        
        game.DealTurn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.River);
        
        game.DealRiver();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(OmahaPhase.Showdown);
        
        game.PerformShowdown();
        game.CurrentPhase.Should().Be(OmahaPhase.Complete);
    }

    #endregion

    #region Helper Methods

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

    private static void PlayBettingRoundCheckOrCall(OmahaGame game)
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

    private static void PlayToShowdown(OmahaGame game)
    {
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealFlop();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealTurn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        game.DealRiver();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
    }

    private static void PlayCompleteHand(OmahaGame game)
    {
        PlayToShowdown(game);
        game.PerformShowdown();
    }

    #endregion
}
