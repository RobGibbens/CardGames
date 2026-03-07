using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Games.HoldTheBaseball;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games.HoldTheBaseball;

public class HoldTheBaseballGameTests
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

        var act = () => new HoldTheBaseballGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 11).Select(i => ($"Player{i}", 1000)).ToList();

        var act = () => new HoldTheBaseballGame(players, smallBlind: 5, bigBlind: 10);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    [Fact]
    public void GameProperties_AreCorrect()
    {
        var game = CreateTwoPlayerGame();

        game.Name.Should().Be("Hold the Baseball");
        game.Description.Should().Contain("3s and 9s are wild");
        game.MinimumNumberOfPlayers.Should().Be(2);
        game.MaximumNumberOfPlayers.Should().Be(10);
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingBlinds()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(Phases.CollectingBlinds);
    }

    [Fact]
    public void CollectBlinds_CollectsFromBothPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectBlinds();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(15); // 5 small blind + 10 big blind
        game.CurrentPhase.Should().Be(Phases.Dealing);
    }

    [Fact]
    public void DealHoleCards_DealsTwoCardsToEachPlayer()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectBlinds();

        game.DealHoleCards();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(2);
        });
        game.CurrentPhase.Should().Be(Phases.PreFlop);
    }

    [Fact]
    public void DealFlop_DealsThreeCommunityCards()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.Flop);

        game.DealFlop();

        game.CommunityCards.Should().HaveCount(3);
    }

    [Fact]
    public void DealTurn_DealsFourthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.Flop);
        game.DealFlop();
        AdvancePhaseForBetting(game, Phases.Turn);

        game.DealTurn();

        game.CommunityCards.Should().HaveCount(4);
    }

    [Fact]
    public void DealRiver_DealsFifthCommunityCard()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.Flop);
        game.DealFlop();
        AdvancePhaseForBetting(game, Phases.Turn);
        game.DealTurn();
        AdvancePhaseForBetting(game, Phases.River);

        game.DealRiver();

        game.CommunityCards.Should().HaveCount(5);
    }

    [Fact]
    public void GetGameRules_ReturnsRulesWithCorrectCode()
    {
        var game = CreateTwoPlayerGame();

        var rules = game.GetGameRules();

        rules.GameTypeCode.Should().Be("HOLDTHEBASEBALL");
        rules.SpecialRules.Should().ContainKey("WildCards");
    }

    [Fact]
    public void PokerGameMetadata_HasCorrectAttributes()
    {
        var attr = typeof(HoldTheBaseballGame)
            .GetCustomAttributes(typeof(PokerGameMetadataAttribute), false)
            .OfType<PokerGameMetadataAttribute>()
            .Single();

        attr.Code.Should().Be("HOLDTHEBASEBALL");
        attr.WildCardRule.Should().Be(WildCardRule.FixedRanks);
        attr.MinimumNumberOfPlayers.Should().Be(2);
        attr.MaximumNumberOfPlayers.Should().Be(10);
        attr.InitialHoleCards.Should().Be(2);
        attr.MaxCommunityCards.Should().Be(5);
    }

    private static HoldTheBaseballGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new HoldTheBaseballGame(players, smallBlind: 5, bigBlind: 10);
    }

    private static HoldTheBaseballGame CreateThreePlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000),
            ("Charlie", 1000)
        };
        return new HoldTheBaseballGame(players, smallBlind: 5, bigBlind: 10);
    }

    /// <summary>
    /// Advances the game through the initial phases up to and including dealing hole cards.
    /// </summary>
    private static void AdvanceToPhase(HoldTheBaseballGame game, Phases target)
    {
        if (game.CurrentPhase == Phases.WaitingToStart)
            game.StartHand();
        if (game.CurrentPhase == Phases.CollectingBlinds && target != Phases.CollectingBlinds)
            game.CollectBlinds();
        if (game.CurrentPhase == Phases.Dealing && target != Phases.Dealing)
            game.DealHoleCards();
        // PreFlop → need to start and complete betting to reach Flop
        if (game.CurrentPhase == Phases.PreFlop && target != Phases.PreFlop)
        {
            game.StartPreFlopBettingRound();
            // Heads-up pre-flop: small blind calls, big blind checks.
            game.ProcessBettingAction(BettingActionType.Call);
            game.ProcessBettingAction(BettingActionType.Check);
        }
    }

    private static void AdvancePhaseForBetting(HoldTheBaseballGame game, Phases target)
    {
        if (game.CurrentPhase == target)
            return;
        game.StartPostFlopBettingRound();
        // Heads-up post-flop rounds: check/check advances the phase.
        game.ProcessBettingAction(BettingActionType.Check);
        game.ProcessBettingAction(BettingActionType.Check);
    }
}
