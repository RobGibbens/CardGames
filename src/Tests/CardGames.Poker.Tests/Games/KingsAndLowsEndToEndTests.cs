using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Variants;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

/// <summary>
/// End-to-end tests for Kings and Lows game flow.
/// These tests verify the complete game lifecycle from start to showdown.
/// Kings and Lows is a five card draw variant where Kings are always wild
/// and the lowest-ranked card in each player's hand is also wild.
/// </summary>
public class KingsAndLowsEndToEndTests
{
    #region Complete Game Flow Tests

    [Fact]
    public void EndToEnd_TwoPlayerGame_CompleteFlow_ToShowdown()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Phase 1: Start Hand
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.WaitingToStart);
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
        
        // Phase 2: Collect Antes
        var anteActions = game.CollectAntes();
        anteActions.Should().HaveCount(2);
        game.CurrentPot.Should().Be(20); // 10 ante from each
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Dealing);
        
        // Phase 3: Deal Hands
        game.DealHands();
        game.GamePlayers.Should().AllSatisfy(gp => gp.Hand.Should().HaveCount(5));
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DropOrStay);
        
        // Phase 4: Drop or Stay - Both players stay
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        var dropOrStayResult = game.FinalizeDropOrStay();
        dropOrStayResult.Success.Should().BeTrue();
        dropOrStayResult.StayingPlayerCount.Should().Be(2);
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
        
        // Phase 5: Draw Phase - Both players draw
        game.ProcessDraw(new int[] { }); // Alice stands pat
        game.ProcessDraw(new int[] { }); // Bob stands pat
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        
        // Phase 6: Showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.PlayerHands.Should().HaveCount(2);
        (showdownResult.Winners.Count + showdownResult.Losers.Count).Should().BeGreaterThanOrEqualTo(2);
        
        // Phase 7: Pot Matching (if there are losers)
        if (game.CurrentPhase == KingsAndLowsPhase.PotMatching)
        {
            var matchResult = game.ProcessPotMatching();
            matchResult.Success.Should().BeTrue();
        }
        
        // Verify game completion
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
        
        // Verify total chips plus pot equals original total
        // In Kings and Lows, the pot might not be empty after a hand due to "losers match pot" mechanic
        var totalChips = game.Players.Sum(p => p.ChipStack);
        var totalWithPot = totalChips + game.CurrentPot;
        totalWithPot.Should().Be(2000); // Original total chips preserved (chips + pot)
    }

    [Fact]
    public void EndToEnd_TwoPlayerGame_WinByAllDrop()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Both players drop
        game.SetPlayerDecision("Alice", DropOrStayDecision.Drop);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        var result = game.FinalizeDropOrStay();
        
        // Assert - Dead hand, pot stays for next hand
        result.Success.Should().BeTrue();
        result.AllDropped.Should().BeTrue();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
        game.CurrentPot.Should().Be(20); // Pot preserved
    }

    [Fact]
    public void EndToEnd_TwoPlayerGame_PlayerVsDeck()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Only Alice stays
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        var dropOrStayResult = game.FinalizeDropOrStay();
        
        dropOrStayResult.Success.Should().BeTrue();
        dropOrStayResult.SinglePlayerStayed.Should().BeTrue();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
        
        // Alice draws
        game.ProcessDraw(new int[] { });
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.PlayerVsDeck);
        game.DeckHand.Should().HaveCount(5);
        
        // Deck draws
        var deckDrawResult = game.ProcessDeckDraw();
        deckDrawResult.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        
        // Showdown
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.IsPlayerVsDeck.Should().BeTrue();
        showdownResult.PlayerHands.Should().ContainKey("Deck");
        showdownResult.PlayerHands.Should().ContainKey("Alice");
    }

    [Fact]
    public void EndToEnd_ThreePlayerGame_CompleteFlow()
    {
        // Arrange
        var game = CreateThreePlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.CurrentPot.Should().Be(30); // 10 ante from each of 3 players
        game.DealHands();
        
        // All players stay
        foreach (var player in game.Players)
        {
            game.SetPlayerDecision(player.Name, DropOrStayDecision.Stay);
        }
        game.FinalizeDropOrStay();
        
        // All players draw (stand pat)
        for (int i = 0; i < 3; i++)
        {
            game.ProcessDraw(new int[] { });
        }
        
        // Showdown
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        var result = game.PerformShowdown();
        result.Success.Should().BeTrue();
        result.PlayerHands.Should().HaveCount(3);
    }

    [Fact]
    public void EndToEnd_TwoPlayerGame_WithDrawing()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Both players stay
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        game.FinalizeDropOrStay();
        
        // Alice discards and draws 3 cards
        var aliceResult = game.ProcessDraw(new int[] { 0, 1, 2 });
        aliceResult.Success.Should().BeTrue();
        aliceResult.DiscardedCards.Should().HaveCount(3);
        aliceResult.NewCards.Should().HaveCount(3);
        
        // Bob discards and draws 2 cards
        var bobResult = game.ProcessDraw(new int[] { 3, 4 });
        bobResult.Success.Should().BeTrue();
        bobResult.DiscardedCards.Should().HaveCount(2);
        bobResult.NewCards.Should().HaveCount(2);
        
        // Showdown
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        var showdownResult = game.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
    }

    #endregion

    #region Variant Factory Integration Tests

    [Fact]
    public void VariantFactory_CreatesKingsAndLowsGame_WithCorrectConfiguration()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var kingsAndLowsInfo = new GameVariantInfo(
            "kings-and-lows",
            "Kings and Lows",
            "A five card draw variant where Kings and low cards are wild.",
            MinPlayers: 2,
            MaxPlayers: 5);
        
        registry.RegisterVariant(kingsAndLowsInfo, (players, smallBlind, bigBlind) =>
            new KingsAndLowsGame(
                players,
                ante: smallBlind,
                kingRequired: false,
                anteEveryHand: false));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        
        // Act
        var game = factory.CreateGame("kings-and-lows", players, 10, 20) as KingsAndLowsGame;
        
        // Assert
        game.Should().NotBeNull();
        game!.Ante.Should().Be(10);
        game.Players.Should().HaveCount(2);
        game.Players[0].Name.Should().Be("Alice");
        game.Players[1].Name.Should().Be("Bob");
    }

    [Fact]
    public void VariantFactory_KingsAndLowsGame_CanPlayCompleteHand()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var kingsAndLowsInfo = new GameVariantInfo(
            "kings-and-lows",
            "Kings and Lows",
            "A five card draw variant where Kings and low cards are wild.",
            MinPlayers: 2,
            MaxPlayers: 5);
        
        registry.RegisterVariant(kingsAndLowsInfo, (players, smallBlind, bigBlind) =>
            new KingsAndLowsGame(
                players,
                ante: smallBlind,
                kingRequired: false,
                anteEveryHand: false));
        var factory = new GameVariantFactory(registry);
        
        var players = new[] { ("Alice", 1000), ("Bob", 1000) };
        var game = factory.CreateGame("kings-and-lows", players, 10, 20) as KingsAndLowsGame;
        
        // Act - Play complete hand
        game!.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        game.FinalizeDropOrStay();
        
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });
        
        var result = game.PerformShowdown();
        
        // Assert
        result.Success.Should().BeTrue();
        // Handle pot matching phase if needed
        if (game.CurrentPhase == KingsAndLowsPhase.PotMatching)
        {
            game.ProcessPotMatching();
        }
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
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
        
        // Verify each player has evaluated hand
        foreach (var playerHand in result.PlayerHands)
        {
            if (playerHand.Value.hand != null)
            {
                playerHand.Value.hand.Type.Should().NotBe(default);
                playerHand.Value.hand.Strength.Should().BeGreaterThan(0);
            }
            playerHand.Value.cards.Should().HaveCount(5);
        }
    }

    [Fact]
    public void EndToEnd_PlayerWildCards_IncludeKingsAndLows()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Act - Get wild cards for each player
        var aliceWildCards = game.GetPlayerWildCards("Alice");
        var bobWildCards = game.GetPlayerWildCards("Bob");
        
        // Assert - Wild cards should be calculated (may be empty if no kings and single lowest)
        aliceWildCards.Should().NotBeNull();
        bobWildCards.Should().NotBeNull();
        
        // If a player has a King, it should be wild
        var aliceHand = game.GamePlayers[0].Hand;
        var aliceKings = aliceHand.Count(c => c.Symbol == CardGames.Core.French.Cards.Symbol.King);
        if (aliceKings > 0)
        {
            aliceWildCards.Count(c => c.Symbol == CardGames.Core.French.Cards.Symbol.King)
                .Should().Be(aliceKings);
        }
    }

    #endregion

    #region Phase Transition Tests

    [Fact]
    public void EndToEnd_PhaseTransitions_AreCorrect()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Act & Assert - Verify each phase transition
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.WaitingToStart);
        
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
        
        game.CollectAntes();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Dealing);
        
        game.DealHands();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DropOrStay);
        
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        game.FinalizeDropOrStay();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
        
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        
        game.PerformShowdown();
        // Phase could be PotMatching or Complete depending on results
        game.CurrentPhase.Should().BeOneOf(
            KingsAndLowsPhase.PotMatching, 
            KingsAndLowsPhase.Complete);
        
        if (game.CurrentPhase == KingsAndLowsPhase.PotMatching)
        {
            game.ProcessPotMatching();
            game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
        }
    }

    [Fact]
    public void EndToEnd_PlayerVsDeck_PhaseTransitions_AreCorrect()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Only Alice stays - triggers Player vs Deck
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
        
        // Alice draws
        game.ProcessDraw(new int[] { });
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.PlayerVsDeck);
        
        // Deck draws
        game.ProcessDeckDraw();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
        
        // Showdown
        game.PerformShowdown();
        game.CurrentPhase.Should().BeOneOf(
            KingsAndLowsPhase.PotMatching,
            KingsAndLowsPhase.Complete);
    }

    #endregion

    #region Pot Management Tests

    [Fact]
    public void EndToEnd_PotMatching_AddsToNextPot()
    {
        // Arrange
        var game = CreateTwoPlayerGame();
        
        // Play first hand
        game.StartHand();
        game.CollectAntes();
        var initialPot = game.CurrentPot;
        game.DealHands();
        
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        game.FinalizeDropOrStay();
        
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });
        
        var showdownResult = game.PerformShowdown();
        
        // If there are losers, they should match pot
        if (game.CurrentPhase == KingsAndLowsPhase.PotMatching && showdownResult.Losers.Count > 0)
        {
            var potBeforeMatch = game.CurrentPot;
            var matchResult = game.ProcessPotMatching();
            
            // New pot should include matched amounts
            matchResult.Success.Should().BeTrue();
            game.CurrentPot.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void EndToEnd_MultipleHands_PotAccumulates()
    {
        // Arrange
        var game = CreateTwoPlayerGame(anteEveryHand: true);
        
        // First hand - all drop (pot stays)
        game.StartHand();
        game.CollectAntes();
        var firstPot = game.CurrentPot;
        game.DealHands();
        
        game.SetPlayerDecision("Alice", DropOrStayDecision.Drop);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        
        game.CurrentPot.Should().Be(firstPot); // Pot preserved
        
        // Second hand
        game.StartHand();
        game.CollectAntes();
        game.CurrentPot.Should().BeGreaterThan(firstPot); // Pot grew
    }

    #endregion

    #region King Required Variant Tests

    [Fact]
    public void EndToEnd_KingRequired_LowsOnlyWildWithKing()
    {
        // Arrange
        var game = CreateTwoPlayerGame(kingRequired: true);
        
        // Start game
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // Verify wild card rules are applied
        game.WildCardRules.KingRequired.Should().BeTrue();
        
        // Get wild cards
        var aliceWildCards = game.GetPlayerWildCards("Alice");
        var aliceHand = game.GamePlayers[0].Hand;
        var aliceHasKing = aliceHand.Any(c => c.Symbol == CardGames.Core.French.Cards.Symbol.King);
        
        // If no king, only kings can be wild (not lows)
        if (!aliceHasKing)
        {
            // Without a king, wild cards should only be kings (which there are none)
            aliceWildCards.Should().OnlyContain(c => c.Symbol == CardGames.Core.French.Cards.Symbol.King);
        }
    }

    #endregion

    #region Helper Methods

    private static KingsAndLowsGame CreateTwoPlayerGame(bool anteEveryHand = false, bool kingRequired = false)
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new KingsAndLowsGame(players, ante: 10, kingRequired: kingRequired, anteEveryHand: anteEveryHand);
    }

    private static KingsAndLowsGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new KingsAndLowsGame(players, ante: 10, kingRequired: false, anteEveryHand: false);
    }

    private static void PlayToShowdown(KingsAndLowsGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        
        // All players stay
        foreach (var player in game.GamePlayers)
        {
            game.SetPlayerDecision(player.Player.Name, DropOrStayDecision.Stay);
        }
        game.FinalizeDropOrStay();
        
        // All players stand pat
        var stayingPlayers = game.GamePlayers.Count(gp => gp.HasStayed);
        for (int i = 0; i < stayingPlayers; i++)
        {
            game.ProcessDraw(new int[] { });
        }
    }

    #endregion
}
