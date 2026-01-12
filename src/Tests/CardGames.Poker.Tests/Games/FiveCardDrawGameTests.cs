using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
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
        game.CurrentPhase.Should().Be(Phases.WaitingToStart);
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

        game.CurrentPhase.Should().Be(Phases.CollectingAntes);
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
        game.CurrentPhase.Should().Be(Phases.Dealing);
    }

    [Fact]
    public void DealHands_DealsFiveCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.DealHands();

        game.GamePlayers.Should().AllSatisfy(gp => gp.Hand.Should().HaveCount(5));
        game.CurrentPhase.Should().Be(Phases.FirstBettingRound);
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

        game.CurrentPhase.Should().Be(Phases.DrawPhase);
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

        game.CurrentPhase.Should().Be(Phases.Showdown);
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
    public void ProcessDraw_CannotDiscardMoreThanThree_WithoutAce()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        // Ensure the current draw player does NOT have an Ace
        var currentDrawPlayer = game.GetCurrentDrawPlayer();
        currentDrawPlayer.SetHand([
            new Card(Suit.Hearts, Symbol.Deuce),
            new Card(Suit.Spades, Symbol.Three),
            new Card(Suit.Diamonds, Symbol.Four),
            new Card(Suit.Clubs, Symbol.Five),
            new Card(Suit.Hearts, Symbol.Six)
        ]);

        var result = game.ProcessDraw(new[] { 0, 1, 2, 3 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("3 cards");
    }

    [Fact]
    public void ProcessDraw_CanDiscardFour_WithAce()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        // Ensure the current draw player HAS an Ace
        var currentDrawPlayer = game.GetCurrentDrawPlayer();
        currentDrawPlayer.SetHand([
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Spades, Symbol.Three),
            new Card(Suit.Diamonds, Symbol.Four),
            new Card(Suit.Clubs, Symbol.Five),
            new Card(Suit.Hearts, Symbol.Six)
        ]);

        var result = game.ProcessDraw(new[] { 0, 1, 2, 3 });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().HaveCount(4);
        result.NewCards.Should().HaveCount(4);
    }

    [Fact]
    public void ProcessDraw_CannotDiscardFive_EvenWithAce()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        // Ensure the current draw player HAS an Ace
        var currentDrawPlayer = game.GetCurrentDrawPlayer();
        currentDrawPlayer.SetHand([
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Spades, Symbol.Three),
            new Card(Suit.Diamonds, Symbol.Four),
            new Card(Suit.Clubs, Symbol.Five),
            new Card(Suit.Hearts, Symbol.Six)
        ]);

        var result = game.ProcessDraw(new[] { 0, 1, 2, 3, 4 });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("4 cards");
    }

    [Fact]
    public void ProcessDraw_CanDiscardFour_WhenDiscardingTheAce()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);

        // Ensure the current draw player HAS an Ace at index 0
        var currentDrawPlayer = game.GetCurrentDrawPlayer();
        currentDrawPlayer.SetHand([
            new Card(Suit.Hearts, Symbol.Ace),
            new Card(Suit.Spades, Symbol.Three),
            new Card(Suit.Diamonds, Symbol.Four),
            new Card(Suit.Clubs, Symbol.Five),
            new Card(Suit.Hearts, Symbol.Six)
        ]);

        // Discard 4 cards including the Ace - eligibility is based on pre-discard hand
        var result = game.ProcessDraw(new[] { 0, 1, 2, 3 });

        result.Success.Should().BeTrue();
        result.DiscardedCards.Should().HaveCount(4);
        // The Ace should be among the discarded cards
        result.DiscardedCards.Should().Contain(c => c.Symbol == Symbol.Ace);
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
        game.CurrentPhase.Should().Be(Phases.Complete);
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

    [Fact]
    public void CanContinue_WhenOnlyOnePlayerHasChips_ReturnsFalse()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 0),
            ("Charlie", 0)
        };
        var game = new FiveCardDrawGame(players, ante: 10, minBet: 20);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void DealerPosition_RotatesAfterShowdown()
    {
        var game = CreateTwoPlayerGame();
        var initialDealerPosition = game.DealerPosition;

        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessDraw([]);
        game.ProcessDraw([]);
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);
        game.PerformShowdown();

        game.DealerPosition.Should().Be((initialDealerPosition + 1) % 2);
    }

    [Fact]
    public void GetPlayersWithChips_ReturnsOnlyPlayersWithPositiveChips()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 0),
            ("Charlie", 500)
        };
        var game = new FiveCardDrawGame(players, ante: 10, minBet: 20);

        var playersWithChips = game.GetPlayersWithChips().ToList();

        playersWithChips.Should().HaveCount(2);
        playersWithChips.Select(p => p.Name).Should().Contain("Alice");
        playersWithChips.Select(p => p.Name).Should().Contain("Charlie");
        playersWithChips.Select(p => p.Name).Should().NotContain("Bob");
    }

    [Fact]
    public void StartHand_ResetsPlayerStatesForNewHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();
        game.DealHands();
        game.ProcessBettingAction(BettingActionType.Fold);
        game.PerformShowdown();

        // Start a new hand
        game.StartHand();

        // Verify player states are reset
        game.Players.Should().AllSatisfy(p =>
        {
            p.HasFolded.Should().BeFalse();
            p.IsAllIn.Should().BeFalse();
        });
        game.CurrentPhase.Should().Be(Phases.CollectingAntes);
    }

    [Fact]
    public void CollectAntes_ShortStackedPlayerGoesAllIn()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 5)  // Less than the ante of 10
        };
        var game = new FiveCardDrawGame(players, ante: 10, minBet: 20);

        game.StartHand();
        var actions = game.CollectAntes();

        // Bob should contribute all 5 chips (his entire stack)
        var bobAction = actions.FirstOrDefault(a => a.PlayerName == "Bob");
        bobAction.Should().NotBeNull();
        bobAction.Amount.Should().Be(5);

        // Bob should have 0 chips remaining
        game.Players.First(p => p.Name == "Bob").ChipStack.Should().Be(0);
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
