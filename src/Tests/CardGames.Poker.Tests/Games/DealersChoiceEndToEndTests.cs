using System;
using CardGames.Poker.Games;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Xunit;
using BettingActionType = CardGames.Poker.Betting.BettingActionType;

namespace CardGames.Poker.Tests.Games;

/// <summary>
/// End-to-end tests for Dealer's Choice game flow.
/// </summary>
public class DealersChoiceEndToEndTests
{
    [Fact]
    public void EndToEnd_DealersChoiceWithHoldem_CompletesSuccessfully()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000), 
            ("Charlie", 1000) 
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);

        // Act - Start hand and select variant
        game.StartHand();
        game.CurrentPhase.Should().Be(DealersChoicePhase.SelectingVariant);
        
        var selectResult = game.SelectVariant(PokerVariant.TexasHoldem);
        selectResult.Success.Should().BeTrue();
        
        // Skip wild cards
        var configResult = game.SkipWildCardConfiguration();
        configResult.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);

        // Get the underlying Hold'em game
        var holdemGame = game.GetCurrentGameAs<HoldEmGame>();
        holdemGame.Should().NotBeNull();

        // Play a basic Hold'em hand
        holdemGame!.StartHand();
        holdemGame.CollectBlinds();
        holdemGame.DealHoleCards();
        holdemGame.StartPreFlopBettingRound();

        // Everyone folds to big blind
        holdemGame.ProcessBettingAction(BettingActionType.Fold);  // UTG
        holdemGame.ProcessBettingAction(BettingActionType.Fold);  // SB

        // Perform showdown (BB wins by fold)
        var showdownResult = holdemGame.PerformShowdown();
        showdownResult.Success.Should().BeTrue();
        showdownResult.WonByFold.Should().BeTrue();

        // Complete the Dealer's Choice hand
        game.CompleteHand();
        game.CurrentPhase.Should().Be(DealersChoicePhase.HandComplete);
        game.HandsPlayed.Should().Be(1);
    }

    [Fact]
    public void EndToEnd_DealersChoiceWithOmaha_CompletesSuccessfully()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000) 
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);

        // Act - Start hand and select Omaha
        game.StartHand();
        game.SelectVariant(PokerVariant.Omaha);
        game.SkipWildCardConfiguration();

        // Get the underlying Omaha game
        var omahaGame = game.GetCurrentGameAs<OmahaGame>();
        omahaGame.Should().NotBeNull();

        // Start and play through the Omaha hand
        omahaGame!.StartHand();
        omahaGame.PostBlinds();
        omahaGame.DealHoleCards();
        omahaGame.StartBettingRound();

        // One player folds
        omahaGame.ProcessBettingAction(BettingActionType.Fold);

        // Showdown
        var showdownResult = omahaGame.PerformShowdown();
        showdownResult.Success.Should().BeTrue();

        // Complete the hand
        game.CompleteHand();
        game.CurrentPhase.Should().Be(DealersChoicePhase.HandComplete);
    }

    [Fact]
    public void EndToEnd_DealersChoiceWithFiveCardDraw_CompletesSuccessfully()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000) 
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);

        // Act - Start hand and select Five Card Draw
        game.StartHand();
        game.SelectVariant(PokerVariant.FiveCardDraw);
        game.SkipWildCardConfiguration();

        // Get the underlying Five Card Draw game
        var drawGame = game.GetCurrentGameAs<FiveCardDrawGame>();
        drawGame.Should().NotBeNull();

        // Start and play through
        drawGame!.StartHand();
        drawGame.CollectAntes();
        drawGame.DealHands();

        // First betting round - all check
        drawGame.ProcessBettingAction(BettingActionType.Check);
        drawGame.ProcessBettingAction(BettingActionType.Check);

        // Draw phase - stand pat
        drawGame.ProcessDraw(Array.Empty<int>());
        drawGame.ProcessDraw(Array.Empty<int>());

        // Second betting round - all check
        drawGame.ProcessBettingAction(BettingActionType.Check);
        drawGame.ProcessBettingAction(BettingActionType.Check);

        // Showdown
        var showdownResult = drawGame.PerformShowdown();
        showdownResult.Success.Should().BeTrue();

        // Complete the hand
        game.CompleteHand();
        game.CurrentPhase.Should().Be(DealersChoicePhase.HandComplete);
    }

    [Fact]
    public void EndToEnd_MultipleHandsWithDifferentVariants()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000) 
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: false);

        // First hand - Hold'em
        game.StartHand();
        game.CurrentDealer.Player.Name.Should().Be("Alice"); // Dealer is at position 0
        game.SelectVariant(PokerVariant.TexasHoldem);
        
        var holdemGame = game.GetCurrentGameAs<HoldEmGame>();
        holdemGame!.StartHand();
        holdemGame.CollectBlinds();
        holdemGame.DealHoleCards();
        holdemGame.StartPreFlopBettingRound();
        holdemGame.ProcessBettingAction(BettingActionType.Fold);
        holdemGame.PerformShowdown();
        
        game.CompleteHand();
        game.HandsPlayed.Should().Be(1);

        // Second hand - Omaha (dealer moved)
        game.StartHand();
        game.CurrentDealer.Player.Name.Should().Be("Bob"); // Dealer moved to position 1
        game.SelectVariant(PokerVariant.Omaha);
        
        var omahaGame = game.GetCurrentGameAs<OmahaGame>();
        omahaGame!.StartHand();
        omahaGame.PostBlinds();
        omahaGame.DealHoleCards();
        omahaGame.StartBettingRound();
        omahaGame.ProcessBettingAction(BettingActionType.Fold);
        omahaGame.PerformShowdown();
        
        game.CompleteHand();
        game.HandsPlayed.Should().Be(2);

        // Third hand - dealer wraps around
        game.StartHand();
        game.CurrentDealer.Player.Name.Should().Be("Alice"); // Wrapped back to position 0
    }

    [Fact]
    public void EndToEnd_DealersChoiceWithWildCards()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000) 
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: true);

        // Act - Select variant and configure wild cards
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.CurrentPhase.Should().Be(DealersChoicePhase.ConfiguringWildCards);

        var wildConfig = new WildCardConfiguration
        {
            Enabled = true,
            Type = WildCardType.DeucesWild
        };
        var configResult = game.ConfigureWildCards(wildConfig);
        configResult.Success.Should().BeTrue();

        // Verify wild card configuration is stored
        game.CurrentHandConfig!.WildCards.Enabled.Should().BeTrue();
        game.CurrentHandConfig.WildCards.Type.Should().Be(WildCardType.DeucesWild);
        game.GetWildCardValues().Should().Contain(2);

        // The underlying game is created
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);
        game.CurrentGame.Should().NotBeNull();
    }

    [Fact]
    public void EndToEnd_DealerRotatesCorrectly()
    {
        // Arrange
        var players = new[] 
        { 
            ("Alice", 1000), 
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: false);

        // Act & Assert - Verify dealer rotation through 3 hands
        game.DealerPosition.Should().Be(0);
        game.CurrentDealer.Player.Name.Should().Be("Alice");

        // Hand 1
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        var holdem1 = game.GetCurrentGameAs<HoldEmGame>();
        holdem1!.StartHand();
        holdem1.CollectBlinds();
        holdem1.DealHoleCards();
        holdem1.StartPreFlopBettingRound();
        holdem1.ProcessBettingAction(BettingActionType.Fold);
        holdem1.ProcessBettingAction(BettingActionType.Fold);
        holdem1.PerformShowdown();
        game.CompleteHand();

        game.DealerPosition.Should().Be(1);
        game.CurrentDealer.Player.Name.Should().Be("Bob");

        // Hand 2
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        var holdem2 = game.GetCurrentGameAs<HoldEmGame>();
        holdem2!.StartHand();
        holdem2.CollectBlinds();
        holdem2.DealHoleCards();
        holdem2.StartPreFlopBettingRound();
        holdem2.ProcessBettingAction(BettingActionType.Fold);
        holdem2.ProcessBettingAction(BettingActionType.Fold);
        holdem2.PerformShowdown();
        game.CompleteHand();

        game.DealerPosition.Should().Be(2);
        game.CurrentDealer.Player.Name.Should().Be("Charlie");

        // Hand 3 - wrap around
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        var holdem3 = game.GetCurrentGameAs<HoldEmGame>();
        holdem3!.StartHand();
        holdem3.CollectBlinds();
        holdem3.DealHoleCards();
        holdem3.StartPreFlopBettingRound();
        holdem3.ProcessBettingAction(BettingActionType.Fold);
        holdem3.ProcessBettingAction(BettingActionType.Fold);
        holdem3.PerformShowdown();
        game.CompleteHand();

        game.DealerPosition.Should().Be(0);
        game.CurrentDealer.Player.Name.Should().Be("Alice");
    }
}
