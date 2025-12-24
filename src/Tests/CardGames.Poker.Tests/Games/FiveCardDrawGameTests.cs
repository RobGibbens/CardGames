using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class FiveCardDrawGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(FiveCardDrawPhase.WaitingToStart);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };
        
        var act = () => new FiveCardDrawGame(players, ante: 10, minBet: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 7).Select(i => ($"Player{i}", 1000)).ToList();
        
        var act = () => new FiveCardDrawGame(players, ante: 10, minBet: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 6 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingAntes()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(FiveCardDrawPhase.CollectingAntes);
    }

    [Fact]
    public void CollectAntes_CollectsFromAllPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectAntes();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(20); // 10 ante from each
        game.CurrentPhase.Should().Be(FiveCardDrawPhase.Dealing);
    }

    [Fact]
    public void DealHands_DealsFiveCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.DealHands();

        game.GamePlayers.Should().AllSatisfy(gp => gp.Hand.Should().HaveCount(5));
        game.CurrentPhase.Should().Be(FiveCardDrawPhase.FirstBettingRound);
    }

    [Fact]
    public void GetAvailableActions_InFirstBettingRound_ReturnsValidActions()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        var available = game.GetAvailableActions();

        available.Should().NotBeNull();
        available.CanCheck.Should().BeTrue();
        available.CanBet.Should().BeTrue();
    }

    [Fact]
    public void ProcessBettingAction_Check_Succeeds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        var result = game.ProcessBettingAction(BettingActionType.Check);

        result.Success.Should().BeTrue();
        result.Action.ActionType.Should().Be(BettingActionType.Check);
    }

    [Fact]
    public void ProcessBettingAction_BothCheck_MovesToDrawPhase()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        game.CurrentPhase.Should().Be(FiveCardDrawPhase.DrawPhase);
    }

    [Fact]
    public void ProcessBettingAction_Fold_ReducesPlayersInHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();

        game.ProcessBettingAction(BettingActionType.Bet, 20);
        game.ProcessBettingAction(BettingActionType.Fold);

        game.CurrentPhase.Should().Be(FiveCardDrawPhase.Showdown);
    }

    [Fact]
    public void ProcessDraw_DiscardsAndDrawsCards()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        var originalHand = game.GamePlayers[1].Hand.ToList();
        var result = game.ProcessDraw(new[] { 0, 1, 2 });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().HaveCount(3);
        result.NewCards.Should().HaveCount(3);
        game.GamePlayers[1].Hand.Should().HaveCount(5);
    }

    [Fact]
    public void ProcessDraw_StandPat_KeepsAllCards()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        var originalHand = game.GamePlayers[1].Hand.ToList();
        var result = game.ProcessDraw(new int[] { });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().BeEmpty();
        result.NewCards.Should().BeEmpty();
    }

    [Fact]
    public void ProcessDraw_CannotDiscardMoreThanThree()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        var result = game.ProcessDraw(new[] { 0, 1, 2, 3 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("3 cards");
    }

    [Fact]
    public void PerformShowdown_ByFold_AwardsPotToLastPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Bet, 20);
        game.ProcessBettingAction(BettingActionType.Fold);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeTrue();
        result.Payouts.Should().HaveCount(1);
        game.CurrentPhase.Should().Be(FiveCardDrawPhase.Complete);
    }

    [Fact]
    public void PerformShowdown_ComparesHands()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);
        // Skip draw
        game.ProcessDraw(new int[] { });
        game.ProcessDraw(new int[] { });
        // Second betting round
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

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
        var game = new FiveCardDrawGame(players, ante: 10, minBet: 20);

        game.CanContinue().Should().BeFalse();
    }

    private static FiveCardDrawGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new FiveCardDrawGame(players, ante: 10, minBet: 20);
    }
}
