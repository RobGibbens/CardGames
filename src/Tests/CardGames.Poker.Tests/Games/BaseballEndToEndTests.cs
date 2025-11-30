using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Variants;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

/// <summary>
/// End-to-end tests for Baseball game flow.
/// These tests verify the complete game lifecycle from start to showdown.
/// Baseball is a seven-card stud variant where 3s and 9s are wild,
/// and 4s dealt face up allow players to buy extra cards.
/// </summary>
public class BaseballEndToEndTests
{
    #region Complete Game Flow Tests

    [Fact]
    public void EndToEnd_TwoPlayerGame_CompleteFlow_ToShowdown()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Phase 1: Start Hand
        game.CurrentPhase.Should().Be(BaseballPhase.WaitingToStart);
        game.StartHand();
        game.CurrentPhase.Should().Be(BaseballPhase.CollectingAntes);
        
        // Phase 2: Collect Antes
        var anteActions = game.CollectAntes();
        anteActions.Should().HaveCount(2);
        game.TotalPot.Should().Be(10); // 5 ante from each
        game.CurrentPhase.Should().Be(BaseballPhase.ThirdStreet);
        
        // Phase 3: Deal Third Street (2 hole cards + 1 board card)
        game.DealThirdStreet();
        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Count.Should().BeGreaterThanOrEqualTo(2);
            gp.BoardCards.Should().HaveCount(1);
        });
        
        // Handle any buy-card offers (decline them for simplicity)
        ClearBuyCardOffers(game);
        
        // Phase 4: Post Bring-In
        var bringInAction = game.PostBringIn();
        bringInAction.ActionType.Should().Be(BettingActionType.Post);
        
        // Phase 5: Third Street Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.FourthStreet);
        
        // Phase 6: Deal Fourth Street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.BoardCards.Should().HaveCount(2);
        });
        
        // Phase 7: Fourth Street Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.FifthStreet);
        
        // Phase 8: Deal Fifth Street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        
        // Phase 9: Fifth Street Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.SixthStreet);
        
        // Phase 10: Deal Sixth Street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        
        // Phase 11: Sixth Street Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.SeventhStreet);
        
        // Phase 12: Deal Seventh Street (face down)
        game.DealStreetCard();
        // No buy-card offers on 7th street (face down)
        
        // Phase 13: Seventh Street Betting
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.Showdown);
        
        // Phase 14: Showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeFalse();
        showdownResult.Payouts.Should().NotBeEmpty();
        showdownResult.PlayerHands.Should().HaveCount(2);
        
        // Verify game completion
        game.CurrentPhase.Should().Be(BaseballPhase.Complete);
        
        // Verify pot was distributed
        var totalChips = game.Players.Sum(p => p.ChipStack);
        totalChips.Should().Be(2000); // Original total chips preserved
    }

    [Fact]
    public void EndToEnd_TwoPlayerGame_WinByFold()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        
        // First betting action - bet
        game.ProcessBettingAction(BettingActionType.Bet, 10);
        
        // Second player folds
        game.ProcessBettingAction(BettingActionType.Fold);
        
        // Should immediately go to showdown
        game.CurrentPhase.Should().Be(BaseballPhase.Showdown);
        
        // Perform showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeTrue();
        showdownResult.Payouts.Should().HaveCount(1);
        
        // Verify game completion
        game.CurrentPhase.Should().Be(BaseballPhase.Complete);
    }

    [Fact]
    public void EndToEnd_ThreePlayerGame_CompleteFlow()
    {
        // Arrange
        var game = CreateThreePlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.TotalPot.Should().Be(15); // 5 ante from each of 3 players
        
        // Deal and play through
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Continue through remaining streets
        for (int street = 0; street < 4; street++) // 4th, 5th, 6th, 7th street
        {
            game.DealStreetCard();
            ClearBuyCardOffers(game);
            game.StartBettingRound();
            PlayBettingRoundCheckOrCall(game);
        }
        
        // Showdown
        game.CurrentPhase.Should().Be(BaseballPhase.Showdown);
        var result = game.PerformShowdown();
        result.Success.Should().BeTrue();
        result.PlayerHands.Should().HaveCount(3);
    }

    #endregion

    #region Variant Factory Integration Tests

    [Fact]
    public void VariantFactory_CreatesBaseballGame_WithCorrectConfiguration()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var baseballInfo = new GameVariantInfo(
            "baseball",
            "Baseball",
            "A seven card stud variant where 3s and 9s are wild.",
            MinPlayers: 2,
            MaxPlayers: 4);
        
        registry.RegisterVariant(baseballInfo, (players, smallBlind, bigBlind) =>
            new BaseballGame(
                players,
                ante: smallBlind,
                bringIn: bigBlind / 2,
                smallBet: bigBlind,
                bigBet: bigBlind * 2,
                buyCardPrice: bigBlind,
                useBringIn: true));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        
        // Act
        var game = factory.CreateGame("baseball", players, 5, 10) as BaseballGame;
        
        // Assert
        game.Should().NotBeNull();
        game!.Ante.Should().Be(5);
        game.SmallBet.Should().Be(10);
        game.BigBet.Should().Be(20);
        game.BuyCardPrice.Should().Be(10);
        game.UseBringIn.Should().BeTrue();
        game.Players.Should().HaveCount(2);
        game.Players[0].Name.Should().Be("Alice");
    }

    [Fact]
    public void VariantFactory_BaseballGame_CanPlayCompleteHand()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var baseballInfo = new GameVariantInfo(
            "baseball",
            "Baseball",
            "A seven card stud variant where 3s and 9s are wild.",
            MinPlayers: 2,
            MaxPlayers: 4);
        
        registry.RegisterVariant(baseballInfo, (players, smallBlind, bigBlind) =>
            new BaseballGame(
                players,
                ante: smallBlind,
                bringIn: bigBlind / 2,
                smallBet: bigBlind,
                bigBet: bigBlind * 2,
                buyCardPrice: bigBlind,
                useBringIn: true));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        var game = factory.CreateGame("baseball", players, 5, 10) as BaseballGame;
        
        // Act - Play complete hand
        game!.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Play through remaining streets
        for (int street = 0; street < 4; street++)
        {
            game.DealStreetCard();
            ClearBuyCardOffers(game);
            game.StartBettingRound();
            PlayBettingRoundCheckOrCall(game);
        }
        
        var result = game.PerformShowdown();
        
        // Assert
        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(BaseballPhase.Complete);
    }

    #endregion

    #region Wild Card Tests

    [Fact]
    public void EndToEnd_WildCards_AreEvaluatedCorrectly()
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
        
        // Verify each player has evaluated hand with potential wild cards
        foreach (var playerHand in result.PlayerHands)
        {
            if (playerHand.Value.hand != null)
            {
                playerHand.Value.hand.Type.Should().NotBe(default);
                playerHand.Value.hand.Strength.Should().BeGreaterThan(0);
            }
            playerHand.Value.cards.Should().HaveCountGreaterThanOrEqualTo(7);
        }
    }

    #endregion

    #region Buy Card Tests

    [Fact]
    public void EndToEnd_BuyCardOffer_CanBeAccepted()
    {
        // This test verifies the buy-card mechanism works
        // Note: We can't guarantee a 4 is dealt, but we test the flow
        
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        
        // Check if there are buy-card offers
        if (game.HasPendingBuyCardOffers())
        {
            var offer = game.GetCurrentBuyCardOffer();
            offer.Should().NotBeNull();
            
            // Accept the offer
            var result = game.ProcessBuyCardDecision(true);
            result.Success.Should().BeTrue();
            result.Purchased.Should().BeTrue();
            result.AmountPaid.Should().Be(game.BuyCardPrice);
            result.ExtraCard.Should().NotBeNull();
        }
        
        // Complete the hand normally
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Continue through remaining streets
        for (int street = 0; street < 4; street++)
        {
            game.DealStreetCard();
            ClearBuyCardOffers(game);
            game.StartBettingRound();
            PlayBettingRoundCheckOrCall(game);
        }
        
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
    }

    [Fact]
    public void EndToEnd_BuyCardOffer_CanBeDeclined()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        
        // Check if there are buy-card offers
        if (game.HasPendingBuyCardOffers())
        {
            var offer = game.GetCurrentBuyCardOffer();
            offer.Should().NotBeNull();
            
            // Decline the offer
            var result = game.ProcessBuyCardDecision(false);
            result.Success.Should().BeTrue();
            result.Purchased.Should().BeFalse();
            result.AmountPaid.Should().Be(0);
        }
        
        // Complete the hand normally
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Continue through remaining streets
        for (int street = 0; street < 4; street++)
        {
            game.DealStreetCard();
            ClearBuyCardOffers(game);
            game.StartBettingRound();
            PlayBettingRoundCheckOrCall(game);
        }
        
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
    }

    #endregion

    #region Phase Transition Tests

    [Fact]
    public void EndToEnd_PhaseTransitions_AreCorrect()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Verify each phase transition
        game.CurrentPhase.Should().Be(BaseballPhase.WaitingToStart);
        
        game.StartHand();
        game.CurrentPhase.Should().Be(BaseballPhase.CollectingAntes);
        
        game.CollectAntes();
        game.CurrentPhase.Should().Be(BaseballPhase.ThirdStreet);
        
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.FourthStreet);
        
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.FifthStreet);
        
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.SixthStreet);
        
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.SeventhStreet);
        
        game.DealStreetCard();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        game.CurrentPhase.Should().Be(BaseballPhase.Showdown);
        
        game.PerformShowdown();
        game.CurrentPhase.Should().Be(BaseballPhase.Complete);
    }

    #endregion

    #region Betting Rules Tests

    [Fact]
    public void EndToEnd_BettingBets_UseCorrectBetSizes()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        
        // Third Street - small bet
        game.GetCurrentMinBet().Should().Be(10);
        
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Fourth Street - small bet
        game.GetCurrentMinBet().Should().Be(10);
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Fifth Street - big bet
        game.GetCurrentMinBet().Should().Be(20);
    }

    #endregion

    #region Helper Methods

    private static BaseballGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);
    }

    private static BaseballGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);
    }

    private static void ClearBuyCardOffers(BaseballGame game)
    {
        while (game.HasPendingBuyCardOffers())
        {
            var offer = game.GetCurrentBuyCardOffer();
            if (offer != null)
            {
                game.ProcessBuyCardDecision(false);
            }
            else
            {
                break;
            }
        }
    }

    private static void PlayBettingRoundCheckOrCall(BaseballGame game)
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

    private static void PlayToShowdown(BaseballGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        ClearBuyCardOffers(game);
        game.PostBringIn();
        game.StartBettingRound();
        PlayBettingRoundCheckOrCall(game);
        
        // Play through remaining streets (4th, 5th, 6th, 7th)
        for (int street = 0; street < 4; street++)
        {
            game.DealStreetCard();
            ClearBuyCardOffers(game);
            game.StartBettingRound();
            PlayBettingRoundCheckOrCall(game);
        }
    }

    #endregion
}
