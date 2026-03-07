using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.IrishHoldEm;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class IrishHoldEmGameTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_CreatesTwoPlayers()
    {
        var game = CreateTwoPlayerGame();

        game.Players.Should().HaveCount(2);
        game.CurrentPhase.Should().Be(Phases.WaitingToStart);
    }

    [Fact]
    public void Constructor_CreatesMaxPlayers()
    {
        var players = Enumerable.Range(1, 10).Select(i => ($"Player{i}", 1000)).ToList();

        var game = new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);

        game.Players.Should().HaveCount(10);
    }

    [Fact]
    public void Constructor_ThrowsForTooFewPlayers()
    {
        var players = new List<(string, int)> { ("Alice", 1000) };

        var act = () => new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 11).Select(i => ($"Player{i}", 1000)).ToList();

        var act = () => new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    #endregion

    #region StartHand / Blinds Tests

    [Fact]
    public void StartHand_SetsPhaseToCollectingBlinds()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(Phases.CollectingBlinds);
    }

    [Fact]
    public void PostBlinds_CollectsSmallAndBigBlinds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.PostBlinds();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(15); // 5 small blind + 10 big blind
        game.CurrentPhase.Should().Be(Phases.PreFlop);
    }

    [Fact]
    public void SmallBlindPosition_IsLeftOfDealer()
    {
        var game = CreateTwoPlayerGame(); // Dealer is position 0

        game.SmallBlindPosition.Should().Be(1);
    }

    [Fact]
    public void BigBlindPosition_IsLeftOfSmallBlind()
    {
        var game = CreateThreePlayerGame(); // Dealer is position 0

        game.BigBlindPosition.Should().Be(2);
    }

    #endregion

    #region Dealing Tests

    [Fact]
    public void DealHoleCards_DealsFourCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.DealHoleCards();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(4);
        });
    }

    [Fact]
    public void DealFlop_DealsThreeCommunityCards()
    {
        var game = CreateTwoPlayerGame();
        SetupToFlop(game);

        game.DealFlop();

        game.CommunityCards.Should().HaveCount(3);
    }

    [Fact]
    public void DealTurn_DealsFourthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToTurn(game);

        game.DealTurn();

        game.CommunityCards.Should().HaveCount(4);
    }

    [Fact]
    public void DealRiver_DealsFifthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        SetupToRiver(game);

        game.DealRiver();

        game.CommunityCards.Should().HaveCount(5);
    }

    #endregion

    #region Betting Tests

    [Fact]
    public void StartBettingRound_CreatesRoundForPreflop()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();

        game.StartBettingRound();

        game.CurrentBettingRound.Should().NotBeNull();
        game.GetCurrentMinBet().Should().Be(10); // big blind
    }

    [Fact]
    public void ProcessBettingAction_Call_Succeeds()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        var available = game.GetAvailableActions();

        if (available.CanCall)
        {
            var result = game.ProcessBettingAction(BettingActionType.Call);
            result.Success.Should().BeTrue();
        }
        else if (available.CanCheck)
        {
            var result = game.ProcessBettingAction(BettingActionType.Check);
            result.Success.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessBettingAction_Fold_RemovesPlayerFromHand()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        // First player can call or raise
        game.ProcessBettingAction(BettingActionType.Raise, 30);

        // Second player folds
        var result = game.ProcessBettingAction(BettingActionType.Fold);

        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(Phases.Showdown);
    }

    [Fact]
    public void GetCurrentMinBet_ReturnsBigBlind()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.GetCurrentMinBet().Should().Be(10);
    }

    #endregion

    #region Phase Progression Tests

    [Fact]
    public void PhaseProgression_AfterFlopBetting_TransitionsToDrawPhase()
    {
        // This is the critical Irish Hold 'Em distinction:
        // After Flop betting completes, phase should be DrawPhase (not Turn)
        var game = CreateTwoPlayerGame();
        SetupToFlop(game);
        game.DealFlop();
        game.StartBettingRound();

        // Complete flop betting
        CompleteBettingRound(game);

        game.CurrentPhase.Should().Be(Phases.DrawPhase);
    }

    [Fact]
    public void PhaseProgression_AfterDiscard_TransitionsToTurn()
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        // All players discard exactly 2 cards
        foreach (var player in game.GamePlayers.Where(p => !p.HasFolded))
        {
            game.DiscardCards(player.Player.Name, new List<int> { 0, 1 });
        }

        game.IsDiscardingComplete().Should().BeTrue();
    }

    [Fact]
    public void FullPhaseProgression_CollectingBlinds_Through_Complete()
    {
        var game = CreateTwoPlayerGame();

        // CollectingBlinds
        game.StartHand();
        game.CurrentPhase.Should().Be(Phases.CollectingBlinds);

        // PreFlop
        game.PostBlinds();
        game.CurrentPhase.Should().Be(Phases.PreFlop);
        game.DealHoleCards();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // Flop
        game.DealFlop();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // DrawPhase (discard) — critical Irish-specific phase
        game.CurrentPhase.Should().Be(Phases.DrawPhase);
        foreach (var player in game.GamePlayers.Where(p => !p.HasFolded))
        {
            game.DiscardCards(player.Player.Name, new List<int> { 0, 1 });
        }

        // Turn
        game.DealTurn();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // River
        game.DealRiver();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // Showdown
        var result = game.PerformShowdown();
        result.Success.Should().BeTrue();
        game.CurrentPhase.Should().Be(Phases.Complete);
    }

    #endregion

    #region Discard Phase Tests

    [Fact]
    public void DiscardCards_WithExactlyTwoIndices_Succeeds()
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        var playerName = game.GamePlayers.First(p => !p.HasFolded).Player.Name;
        game.DiscardCards(playerName, new List<int> { 0, 1 });

        var player = game.GamePlayers.First(p => p.Player.Name == playerName);
        player.HasDiscarded.Should().BeTrue();
        player.HoleCards.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0)]   // Zero discards — must discard exactly 2
    [InlineData(1)]   // One discard — must discard exactly 2
    [InlineData(3)]   // Three discards — must discard exactly 2
    public void DiscardCards_WithWrongNumberOfIndices_Throws(int discardCount)
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        var playerName = game.GamePlayers.First(p => !p.HasFolded).Player.Name;
        var indices = Enumerable.Range(0, discardCount).ToList();

        var act = () => game.DiscardCards(playerName, indices);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*exactly 2*");
    }

    [Theory]
    [InlineData(-1, 0)]   // Negative index
    [InlineData(0, 4)]    // Index >= 4 (out of bounds for 4-card hand)
    [InlineData(0, 5)]    // Well beyond bounds
    [InlineData(-2, -1)]  // Both negative
    public void DiscardCards_WithInvalidIndices_Throws(int idx1, int idx2)
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        var playerName = game.GamePlayers.First(p => !p.HasFolded).Player.Name;

        var act = () => game.DiscardCards(playerName, new List<int> { idx1, idx2 });

        act.Should().Throw<System.ArgumentException>();
    }

    [Fact]
    public void DiscardCards_AfterDiscard_PlayerHasExactlyTwoHoleCards()
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        var playerName = game.GamePlayers.First(p => !p.HasFolded).Player.Name;
        var player = game.GamePlayers.First(p => p.Player.Name == playerName);

        // Before discard: 4 hole cards
        player.HoleCards.Should().HaveCount(4);

        game.DiscardCards(playerName, new List<int> { 2, 3 });

        // After discard: exactly 2 hole cards remain
        player.HoleCards.Should().HaveCount(2);
    }

    [Fact]
    public void IsDiscardingComplete_BeforeAnyDiscard_ReturnsFalse()
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        game.IsDiscardingComplete().Should().BeFalse();
    }

    [Fact]
    public void IsDiscardingComplete_AfterOnePlayerDiscards_ReturnsFalse()
    {
        var game = CreateThreePlayerGame();
        SetupToDrawPhase(game);

        // Only first active player discards
        var firstName = game.GamePlayers.First(p => !p.HasFolded).Player.Name;
        game.DiscardCards(firstName, new List<int> { 0, 1 });

        game.IsDiscardingComplete().Should().BeFalse();
    }

    [Fact]
    public void IsDiscardingComplete_AfterAllActivePlayersDiscard_ReturnsTrue()
    {
        var game = CreateTwoPlayerGame();
        SetupToDrawPhase(game);

        foreach (var player in game.GamePlayers.Where(p => !p.HasFolded))
        {
            game.DiscardCards(player.Player.Name, new List<int> { 0, 1 });
        }

        game.IsDiscardingComplete().Should().BeTrue();
    }

    [Fact]
    public void IsDiscardingComplete_FoldedPlayersDoNotNeedToDiscard()
    {
        var game = CreateThreePlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        // One player folds during pre-flop
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Fold);
        game.ProcessBettingAction(BettingActionType.Call);

        // Flop
        game.DealFlop();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // Now in DrawPhase — only 2 active players remain
        game.CurrentPhase.Should().Be(Phases.DrawPhase);

        // Both remaining active players discard
        foreach (var player in game.GamePlayers.Where(p => !p.HasFolded))
        {
            game.DiscardCards(player.Player.Name, new List<int> { 0, 1 });
        }

        game.IsDiscardingComplete().Should().BeTrue();
    }

    #endregion

    #region Showdown Tests

    [Fact]
    public void PerformShowdown_ByFold_AwardsPotToLastPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        game.ProcessBettingAction(BettingActionType.Raise, 30);
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
        PlayFullHandToShowdown(game);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeFalse();
        result.Payouts.Should().NotBeEmpty();
        result.PlayerHands.Should().HaveCount(2);
    }

    [Fact]
    public void PerformShowdown_PostDiscard_UsesHoldemHandEvaluation()
    {
        // After discarding, each player should have exactly 2 hole cards,
        // and showdown should use HoldemHand evaluation (0-2 hole cards + community, best 5)
        var game = CreateTwoPlayerGame();
        PlayFullHandToShowdown(game);

        var result = game.PerformShowdown();

        result.Success.Should().BeTrue();

        // Verify post-discard players have 2 hole cards in the showdown hand data
        foreach (var (_, handData) in result.PlayerHands)
        {
            handData.holeCards.Should().HaveCount(2,
                "Irish Hold 'Em players should have exactly 2 hole cards at showdown after discarding");
        }
    }

    [Fact]
    public void FoldDuringFlopBetting_SkipsDiscardAndAwardsPot()
    {
        // If all but one player fold during flop betting, skip discard and award pot
        var game = CreateTwoPlayerGame();
        SetupToFlop(game);
        game.DealFlop();
        game.StartBettingRound();

        // Big bet → fold → only one player left
        game.ProcessBettingAction(BettingActionType.Bet, 100);
        game.ProcessBettingAction(BettingActionType.Fold);

        // Should go to showdown (fold win), not DrawPhase
        var result = game.PerformShowdown();
        result.Success.Should().BeTrue();
        result.WonByFold.Should().BeTrue();
        game.CurrentPhase.Should().Be(Phases.Complete);
    }

    #endregion

    #region Misc Tests

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
        var game = new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);

        game.CanContinue().Should().BeFalse();
    }

    [Fact]
    public void GetCurrentStreetName_ReturnsCorrectName()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.PostBlinds();

        game.GetCurrentStreetName().Should().Be("Preflop");
    }

    [Fact]
    public void DealerMoves_AfterHandComplete()
    {
        var game = CreateTwoPlayerGame();
        var initialDealerPosition = game.DealerPosition;

        // Play a hand to completion via fold
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();
        game.ProcessBettingAction(BettingActionType.Raise, 30);
        game.ProcessBettingAction(BettingActionType.Fold);
        game.PerformShowdown();

        game.DealerPosition.Should().NotBe(initialDealerPosition);
    }

    [Fact]
    public void GetDealer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();

        var dealer = game.GetDealer();

        dealer.Should().NotBeNull();
        dealer.Player.Name.Should().Be("Alice");
    }

    [Fact]
    public void GetSmallBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateTwoPlayerGame();

        var sbPlayer = game.GetSmallBlindPlayer();

        sbPlayer.Should().NotBeNull();
        sbPlayer.Player.Name.Should().Be("Bob"); // Left of dealer
    }

    [Fact]
    public void GetBigBlindPlayer_ReturnsCorrectPlayer()
    {
        var game = CreateThreePlayerGame();

        var bbPlayer = game.GetBigBlindPlayer();

        bbPlayer.Should().NotBeNull();
        bbPlayer.Player.Name.Should().Be("Charlie"); // Left of small blind
    }

    #endregion

    #region Helpers

    private static IrishHoldEmGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static IrishHoldEmGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new IrishHoldEmGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static void CompleteBettingRound(IrishHoldEmGame game)
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

    private static void SetupToFlop(IrishHoldEmGame game)
    {
        game.StartHand();
        game.PostBlinds();
        game.DealHoleCards();
        game.StartBettingRound();

        // Complete preflop betting (everyone calls/checks)
        CompleteBettingRound(game);
    }

    private static void SetupToDrawPhase(IrishHoldEmGame game)
    {
        SetupToFlop(game);

        // Flop
        game.DealFlop();
        game.StartBettingRound();
        CompleteBettingRound(game);

        // Should now be in DrawPhase
    }

    private static void SetupToTurn(IrishHoldEmGame game)
    {
        SetupToDrawPhase(game);

        // Discard phase — all active players discard 2 cards
        foreach (var player in game.GamePlayers.Where(p => !p.HasFolded))
        {
            game.DiscardCards(player.Player.Name, new List<int> { 0, 1 });
        }
    }

    private static void SetupToRiver(IrishHoldEmGame game)
    {
        SetupToTurn(game);

        // Turn
        game.DealTurn();
        game.StartBettingRound();
        CompleteBettingRound(game);
    }

    private static void PlayFullHandToShowdown(IrishHoldEmGame game)
    {
        SetupToRiver(game);

        // River
        game.DealRiver();
        game.StartBettingRound();
        CompleteBettingRound(game);
    }

    #endregion
}
