using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.SevenCardStud;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class SevenCardStudGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(SevenCardStudPhase.WaitingToStart);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };
        
        var act = () => new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 8).Select(i => ($"Player{i}", 1000)).ToList();
        
        var act = () => new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 7 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingAntes()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(SevenCardStudPhase.CollectingAntes);
    }

    [Fact]
    public void CollectAntes_CollectsFromAllPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectAntes();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(10); // 5 ante from each
        game.CurrentPhase.Should().Be(SevenCardStudPhase.ThirdStreet);
    }

    [Fact]
    public void DealThirdStreet_DealsTwoHoleAndOneBoardToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.DealThirdStreet();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(2);
            gp.BoardCards.Should().HaveCount(1);
        });
    }

    [Fact]
    public void GetBringInPlayer_ReturnsPlayerWithLowestUpcard()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        var bringInPlayer = game.GetBringInPlayer();

        bringInPlayer.Should().NotBeNull();
        // The bring-in player should have the lowest board card
        // Suit order for ties: Clubs (lowest) < Diamonds < Hearts < Spades (highest)
        var lowestBoardCard = game.GamePlayers
            .Select(gp => gp.BoardCards.First())
            .OrderBy(c => c.Value)
            .ThenBy(c => GetSuitRank(c.Suit))
            .First();
        bringInPlayer.BoardCards.First().Should().Be(lowestBoardCard);
    }

    private static int GetSuitRank(Suit suit)
    {
        return suit switch
        {
            Suit.Clubs => 0,
            Suit.Diamonds => 1,
            Suit.Hearts => 2,
            Suit.Spades => 3,
            _ => 0
        };
    }

    [Fact]
    public void PostBringIn_AddsChipsToPot()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        var action = game.PostBringIn();

        action.ActionType.Should().Be(BettingActionType.Post);
        action.Amount.Should().Be(5); // bring-in amount
        game.TotalPot.Should().Be(15); // 10 antes + 5 bring-in
    }

    [Fact]
    public void StartBettingRound_CreatesRoundWithCorrectMinBet()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        game.PostBringIn();

        game.StartBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
        game.GetCurrentMinBet().Should().Be(10); // small bet
    }

    [Fact]
    public void ProcessBettingAction_Check_Succeeds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        game.PostBringIn();
        game.StartBettingRound();

        // The bring-in player already has chips in, so let them check/call
        // First player can complete to full bet or call
        var available = game.GetAvailableActions();
        
        // If they can check, check
        if (available.CanCheck)
        {
            var result = game.ProcessBettingAction(BettingActionType.Check);
            result.Success.Should().BeTrue();
        }
        else if (available.CanCall)
        {
            var result = game.ProcessBettingAction(BettingActionType.Call);
            result.Success.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessBettingAction_Fold_RemovesPlayerFromHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        game.PostBringIn();
        game.StartBettingRound();

        // First, bet
        game.ProcessBettingAction(BettingActionType.Bet, 10);
        
        // Then fold
        var result = game.ProcessBettingAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(SevenCardStudPhase.Showdown);
    }

    [Fact]
    public void DealStreetCard_OnFourthStreet_AddsOneBoard()
    {
        var game = CreateTwoPlayerGame();
        SetupToFourthStreet(game);

        game.DealStreetCard();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.BoardCards.Should().HaveCount(2);
        });
    }

    [Fact]
    public void DealStreetCard_OnSeventhStreet_AddsOneHoleCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToSeventhStreet(game);

        game.DealStreetCard();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(3);
            gp.BoardCards.Should().HaveCount(4);
        });
    }

    [Fact]
    public void PerformShowdown_ByFold_AwardsPotToLastPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        game.PostBringIn();
        game.StartBettingRound();
        game.ProcessBettingAction(BettingActionType.Bet, 10);
        game.ProcessBettingAction(BettingActionType.Fold);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeTrue();
        result.Payouts.Should().HaveCount(1);
        game.CurrentPhase.Should().Be(SevenCardStudPhase.Complete);
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
    public void GetCurrentMinBet_ReturnsSmallBetOnThirdStreet()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.GetCurrentMinBet().Should().Be(10);
    }

    [Fact]
    public void GetCurrentMinBet_ReturnsBigBetOnFifthStreet()
    {
        var game = CreateTwoPlayerGame();
        SetupToFifthStreet(game);

        game.GetCurrentMinBet().Should().Be(20);
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
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void GetCurrentStreetName_ReturnsCorrectName()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.GetCurrentStreetName().Should().Be("Third Street");
    }

    [Fact]
    public void UseBringIn_DefaultsToTrue()
    {
        var game = CreateTwoPlayerGame();

        game.UseBringIn.Should().BeTrue();
    }

    [Fact]
    public void UseBringIn_CanBeSetToFalse()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, useBringIn: false);

        game.UseBringIn.Should().BeFalse();
    }

    [Fact]
    public void GetBringInPlayer_ReturnsNull_WhenBringInDisabled()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        var bringInPlayer = game.GetBringInPlayer();

        bringInPlayer.Should().BeNull();
    }

    [Fact]
    public void PostBringIn_ThrowsException_WhenBringInDisabled()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        var act = () => game.PostBringIn();

        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*Bring-in is disabled*");
    }

    [Fact]
    public void StartBettingRound_WorksWithoutBringIn()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // Should not throw and should start betting round without bring-in
        game.StartBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
        game.TotalPot.Should().Be(10); // Only antes, no bring-in
    }

    [Fact]
    public void PlayFullHand_WorksWithoutBringIn()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // Skip PostBringIn - directly start betting round
        game.StartBettingRound();

        // Complete third street betting
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

        // Verify game progressed to fourth street
        game.CurrentPhase.Should().Be(SevenCardStudPhase.FourthStreet);
    }

    private static SevenCardStudGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new SevenCardStudGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);
    }

    private static void SetupToFourthStreet(SevenCardStudGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        game.PostBringIn();
        game.StartBettingRound();
        
        // Complete third street betting (everyone checks/calls)
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

    private static void SetupToFifthStreet(SevenCardStudGame game)
    {
        SetupToFourthStreet(game);
        
        // Fourth street
        game.DealStreetCard();
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

    private static void SetupToSeventhStreet(SevenCardStudGame game)
    {
        SetupToFifthStreet(game);
        
        // Fifth street
        game.DealStreetCard();
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
        
        // Sixth street
        game.DealStreetCard();
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

    private static void PlayFullHandToShowdown(SevenCardStudGame game)
    {
        SetupToSeventhStreet(game);
        
        // Seventh street
        game.DealStreetCard();
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
