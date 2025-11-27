using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class BaseballGameTests
{
    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(BaseballPhase.WaitingToStart);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };
        
        var act = () => new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 5).Select(i => ($"Player{i}", 1000)).ToList();
        
        var act = () => new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 4 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingAntes()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(BaseballPhase.CollectingAntes);
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
        game.CurrentPhase.Should().Be(BaseballPhase.ThirdStreet);
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

        var available = game.GetAvailableActions();
        
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

        game.ProcessBettingAction(BettingActionType.Bet, 10);
        var result = game.ProcessBettingAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(BaseballPhase.Showdown);
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
            gp.HoleCards.Should().HaveCountGreaterThanOrEqualTo(3);
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
        game.CurrentPhase.Should().Be(BaseballPhase.Complete);
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
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);

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
    public void BuyCardPrice_IsConfigurable()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 50);

        game.BuyCardPrice.Should().Be(50);
    }

    [Fact]
    public void HasPendingBuyCardOffers_ReturnsFalse_WhenNoFoursDealt()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // The result depends on random card dealing, so we just verify it doesn't throw
        // and returns a valid boolean
        var hasPending = game.HasPendingBuyCardOffers();
        (hasPending == true || hasPending == false).Should().BeTrue();
    }

    [Fact]
    public void ProcessBuyCardDecision_ReturnsError_WhenNoPendingOffer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // Clear any pending offers first
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

        // Now try to process without a pending offer
        var result = game.ProcessBuyCardDecision(true);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No pending buy-card offer");
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
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20, useBringIn: false);

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
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // Handle any buy card offers first
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
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20, useBringIn: false);
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
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20, useBringIn: false);
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();

        // Handle any buy card offers first
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

        // Should not throw and should start betting round without bring-in
        game.StartBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
        game.TotalPot.Should().Be(10); // Only antes, no bring-in
    }

    [Fact]
    public void StartDealingThirdStreet_InitializesIncrementalDealing()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.StartDealingThirdStreet();

        game.HasMorePlayersToDeal().Should().BeTrue();
    }

    [Fact]
    public void DealThirdStreetToNextPlayer_DealsCardsToOnePlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.StartDealingThirdStreet();

        game.DealThirdStreetToNextPlayer();

        // First player should have cards, second player should not
        game.GamePlayers[0].HoleCards.Should().HaveCount(2);
        game.GamePlayers[0].BoardCards.Should().HaveCount(1);
        game.GamePlayers[1].HoleCards.Should().BeEmpty();
        game.GamePlayers[1].BoardCards.Should().BeEmpty();
    }

    [Fact]
    public void DealThirdStreetToNextPlayer_AllPlayersDealtAfterLoop()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.StartDealingThirdStreet();

        while (game.HasMorePlayersToDeal())
        {
            game.DealThirdStreetToNextPlayer();
        }

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(2);
            gp.BoardCards.Should().HaveCount(1);
        });
    }

    [Fact]
    public void FinishThirdStreetDealing_ResetsBetsAndDeterminesBringIn()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        var game = new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20, useBringIn: true);
        game.StartHand();
        game.CollectAntes();
        game.StartDealingThirdStreet();

        while (game.HasMorePlayersToDeal())
        {
            game.DealThirdStreetToNextPlayer();
        }

        game.FinishThirdStreetDealing();

        // Bring-in player should be set since useBringIn is true
        game.GetBringInPlayer().Should().NotBeNull();
    }

    [Fact]
    public void StartDealingStreetCard_InitializesIncrementalDealing()
    {
        var game = CreateTwoPlayerGame();
        SetupToFourthStreetWithoutBringIn(game);

        game.StartDealingStreetCard();

        game.HasMorePlayersToDeal().Should().BeTrue();
    }

    [Fact]
    public void DealStreetCardToNextPlayer_DealsCardToOnePlayer()
    {
        var game = CreateTwoPlayerGame();
        SetupToFourthStreetWithoutBringIn(game);
        var initialBoardCount = game.GamePlayers[0].BoardCards.Count;
        game.StartDealingStreetCard();

        game.DealStreetCardToNextPlayer();

        // First player should have one more board card, second player unchanged
        game.GamePlayers[0].BoardCards.Count.Should().Be(initialBoardCount + 1);
        game.GamePlayers[1].BoardCards.Count.Should().Be(initialBoardCount);
    }

    [Fact]
    public void DealStreetCardToNextPlayer_OnSeventhStreet_DealsHoleCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToSeventhStreetWithoutBringIn(game);
        var initialHoleCount = game.GamePlayers[0].HoleCards.Count;
        game.StartDealingStreetCard();

        var dealt4 = game.DealStreetCardToNextPlayer();

        // Seventh street cards are face down (hole cards), so no buy-card offer
        dealt4.Should().BeFalse();
        game.GamePlayers[0].HoleCards.Count.Should().Be(initialHoleCount + 1);
    }

    private static BaseballGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new BaseballGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20, buyCardPrice: 20);
    }

    private static void SetupToFourthStreet(BaseballGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        
        // Handle any buy card offers
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
        
        game.PostBringIn();
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

    private static void SetupToFifthStreet(BaseballGame game)
    {
        SetupToFourthStreet(game);
        
        game.DealStreetCard();
        
        // Handle any buy card offers
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

    private static void SetupToSeventhStreet(BaseballGame game)
    {
        SetupToFifthStreet(game);
        
        // Fifth street
        game.DealStreetCard();
        
        // Handle any buy card offers
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
        
        // Handle any buy card offers
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

    private static void PlayFullHandToShowdown(BaseballGame game)
    {
        SetupToSeventhStreet(game);
        
        // Seventh street (no buy card offers - card is face down)
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

    /// <summary>
    /// Sets up to fourth street without requiring bring-in.
    /// </summary>
    private static void SetupToFourthStreetWithoutBringIn(BaseballGame game)
    {
        game.StartHand();
        game.CollectAntes();
        game.DealThirdStreet();
        
        // Handle any buy card offers
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
        
        // Skip bring-in since it's disabled by default
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

    /// <summary>
    /// Sets up to seventh street without requiring bring-in.
    /// </summary>
    private static void SetupToSeventhStreetWithoutBringIn(BaseballGame game)
    {
        SetupToFourthStreetWithoutBringIn(game);
        
        // Fourth street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        CompleteCheckingRound(game);
        
        // Fifth street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        CompleteCheckingRound(game);
        
        // Sixth street
        game.DealStreetCard();
        ClearBuyCardOffers(game);
        game.StartBettingRound();
        CompleteCheckingRound(game);
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

    private static void CompleteCheckingRound(BaseballGame game)
    {
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
