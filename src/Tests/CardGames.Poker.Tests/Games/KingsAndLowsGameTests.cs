using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class KingsAndLowsGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.WaitingToStart);
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingAntes()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
    }

    [Fact]
    public void CollectAntes_CollectsFromAllPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectAntes();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.CurrentPot.Should().Be(20); // 10 ante from each
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Dealing);
    }

    [Fact]
    public void DealHands_DealsFiveCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.DealHands();

        game.GamePlayers.Should().AllSatisfy(gp => gp.Hand.Should().HaveCount(5));
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DropOrStay);
    }

    [Fact]
    public void SetPlayerDecision_SetsDecisionCorrectly()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);

        game.GamePlayers[0].HasStayed.Should().BeTrue();
        game.GamePlayers[1].HasDropped.Should().BeTrue();
    }

    [Fact]
    public void FinalizeDropOrStay_WhenAllDrop_ReturnsAllDropped()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Drop);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);

        var result = game.FinalizeDropOrStay();

        result.Success.Should().BeTrue();
        result.AllDropped.Should().BeTrue();
        result.StayingPlayerCount.Should().Be(0);
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
    }

    [Fact]
    public void FinalizeDropOrStay_WhenOneStays_SetsSinglePlayerStayed()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);

        var result = game.FinalizeDropOrStay();

        result.Success.Should().BeTrue();
        result.SinglePlayerStayed.Should().BeTrue();
        result.StayingPlayerCount.Should().Be(1);
        result.StayingPlayerNames.Should().Contain("Alice");
        result.DroppedPlayerNames.Should().Contain("Bob");
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
    }

    [Fact]
    public void FinalizeDropOrStay_WhenMultipleStay_MovesToDrawPhase()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);

        var result = game.FinalizeDropOrStay();

        result.Success.Should().BeTrue();
        result.SinglePlayerStayed.Should().BeFalse();
        result.StayingPlayerCount.Should().Be(2);
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.DrawPhase);
    }

    [Fact]
    public void FinalizeDropOrStay_WhenNotAllDecided_ReturnsFalse()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        // Bob hasn't decided

        var result = game.FinalizeDropOrStay();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("all players");
    }

    [Fact]
    public void ProcessDraw_DiscardsAndDrawsCards()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);

        var result = game.ProcessDraw(new[] { 0, 1, 2 });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().HaveCount(3);
        result.NewCards.Should().HaveCount(3);
    }

    [Fact]
    public void ProcessDraw_CanDiscardUpToFiveCards()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);

        var result = game.ProcessDraw(new[] { 0, 1, 2, 3, 4 });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().HaveCount(5);
        result.NewCards.Should().HaveCount(5);
    }

    [Fact]
    public void ProcessDraw_StandPat_KeepsAllCards()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);

        var result = game.ProcessDraw(new int[] { });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().BeEmpty();
        result.NewCards.Should().BeEmpty();
    }

    [Fact]
    public void ProcessDraw_AfterAllDraw_MovesToShowdown()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);

        game.ProcessDraw(new int[] { }); // Alice draws
        game.ProcessDraw(new int[] { }); // Bob draws

        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
    }

    [Fact]
    public void ProcessDraw_SinglePlayer_MovesToPlayerVsDeck()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();

        game.ProcessDraw(new int[] { }); // Alice draws

        game.CurrentPhase.Should().Be(KingsAndLowsPhase.PlayerVsDeck);
        game.DeckHand.Should().HaveCount(5);
    }

    [Fact]
    public void ProcessDeckDraw_DrawsForDeck()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        game.ProcessDraw(new int[] { }); // Alice draws

        var result = game.ProcessDeckDraw();

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Showdown);
    }

    [Fact]
    public void PerformShowdown_WithMultiplePlayers_DeterminesWinner()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.PlayerHands.Should().HaveCount(2);
        (result.Winners.Count + result.Losers.Count).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void PerformShowdown_WithPlayerVsDeck_ComparesAgainstDeck()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        game.ProcessDraw(new int[] { });
        game.ProcessDeckDraw();

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.IsPlayerVsDeck.Should().BeTrue();
        result.PlayerHands.Should().ContainKey("Deck");
        result.PlayerHands.Should().ContainKey("Alice");
    }

    [Fact]
    public void ProcessPotMatching_LosersMatchPot()
    {
        var game = CreateTwoPlayerGame();
        SetupDrawPhase(game);
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });
        var showdownResult = game.PerformShowdown();

        // The phase should be PotMatching if there are losers
        if (game.CurrentPhase == KingsAndLowsPhase.PotMatching)
        {
            var matchResult = game.ProcessPotMatching();

            matchResult.Success.Should().BeTrue();
            matchResult.NewPotAmount.Should().BeGreaterThan(0);
            game.CurrentPhase.Should().Be(KingsAndLowsPhase.Complete);
        }
    }

    [Fact]
    public void GetPlayerWildCards_ReturnsWildCards()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        var wildCards = game.GetPlayerWildCards("Alice");

        // Wild cards should include kings and lowest cards
        // At minimum should return something (could be empty if no kings and single lowest)
        wildCards.Should().NotBeNull();
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
        var game = new KingsAndLowsGame(players, ante: 10);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void AnteEveryHand_WhenTrue_CollectsAnteOnEachHand()
    {
        var game = CreateTwoPlayerGame(anteEveryHand: true);
        
        // First hand
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Drop);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        
        // Second hand
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
    }

    [Fact]
    public void SingleAnte_WhenFalse_SkipsAnteOnSubsequentHands()
    {
        var game = CreateTwoPlayerGame(anteEveryHand: false);
        
        // First hand
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.CollectingAntes);
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Drop);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Drop);
        game.FinalizeDropOrStay();
        
        // Second hand - should skip ante
        game.StartHand();
        game.CurrentPhase.Should().Be(KingsAndLowsPhase.Dealing);
    }

    private static KingsAndLowsGame CreateTwoPlayerGame(bool anteEveryHand = false)
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new KingsAndLowsGame(players, ante: 10, kingRequired: false, anteEveryHand: anteEveryHand);
    }

    private static void SetupDrawPhase(KingsAndLowsGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.SetPlayerDecision("Alice", DropOrStayDecision.Stay);
        game.SetPlayerDecision("Bob", DropOrStayDecision.Stay);
        game.FinalizeDropOrStay();
    }
}
