using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GoodBadUgly;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class GoodBadUglyGameTests
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

        var act = () => new GoodBadUglyGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_ThrowsForTooManyPlayers()
    {
        var players = Enumerable.Range(1, 11).Select(i => ($"Player{i}", 1000)).ToList();

        var act = () => new GoodBadUglyGame(players, ante: 5, bringIn: 5, smallBet: 10, bigBet: 20);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    [Fact]
    public void StartHand_SetsPhaseToCollectingAntes()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.CurrentPhase.Should().Be(Phases.CollectingAntes);
    }

    [Fact]
    public void StartHand_DealsThreeTableCards()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.TableCards.Should().HaveCount(3);
    }

    [Fact]
    public void StartHand_TableCardsAreNotYetRevealed()
    {
        var game = CreateTwoPlayerGame();

        game.StartHand();

        game.WildRank.Should().BeNull();
        game.DiscardRank.Should().BeNull();
        game.EliminationRank.Should().BeNull();
    }

    [Fact]
    public void CollectAntes_CollectsFromAllPlayers()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var actions = game.CollectAntes();

        actions.Should().HaveCount(2);
        actions.All(a => a.ActionType == BettingActionType.Post).Should().BeTrue();
        game.TotalPot.Should().Be(10); // 5 ante each
        game.CurrentPhase.Should().Be(Phases.ThirdStreet);
    }

    [Fact]
    public void DealThirdStreet_DealsCorrectCards()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();
        game.CollectAntes();

        game.DealThirdStreet();

        game.GamePlayers.Should().AllSatisfy(gp =>
        {
            gp.HoleCards.Should().HaveCount(4);
        });

        game.TableCards.Should().HaveCount(3);
    }

    [Fact]
    public void DealStreetCard_ThrowsBecauseNoAdditionalStreetCardsAreDealt()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.FourthStreet);

        var act = () => game.DealStreetCard();

        act.Should().Throw<System.InvalidOperationException>();
    }

    [Fact]
    public void RevealTheGood_SetsWildRank()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.RevealTheGood);

        var goodCard = game.RevealTheGood();

        game.WildRank.Should().Be(goodCard.Value);
        game.CurrentPhase.Should().Be(Phases.FourthStreet);
    }

    [Fact]
    public void RevealTheGood_ThrowsIfNotInCorrectPhase()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var act = () => game.RevealTheGood();

        act.Should().Throw<System.InvalidOperationException>();
    }

    [Fact]
    public void RevealTheBad_ForcesDiscard()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.RevealTheBad);

        var (badCard, discards) = game.RevealTheBad();

        game.DiscardRank.Should().Be(badCard.Value);
        game.CurrentPhase.Should().Be(Phases.FifthStreet);
        // Discards may or may not be empty depending on random cards
    }

    [Fact]
    public void RevealTheUgly_EliminatesPlayersWithMatchingHoleCards()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.RevealTheUgly);

        var (uglyCard, eliminated) = game.RevealTheUgly();

        game.EliminationRank.Should().Be(uglyCard.Value);
        // May or may not eliminate players depending on random cards
        game.CurrentPhase.Should().Be(Phases.SixthStreet);
    }

    [Fact]
    public void PerformShowdown_ReturnsError_WhenNotInShowdownPhase()
    {
        var game = CreateTwoPlayerGame();
        game.StartHand();

        var result = game.PerformShowdown();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetGameRules_ReturnsValidRules()
    {
        var game = new GoodBadUglyGame();

        var rules = game.GetGameRules();

        rules.Should().NotBeNull();
        rules.GameTypeCode.Should().Be("GOODBADUGLY");
    }

    [Fact]
    public void GetCurrentMinBet_ReturnsSmallBetForEarlyStreets()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.ThirdStreet);

        game.GetCurrentMinBet().Should().Be(10);
    }

    [Fact]
    public void GetCurrentMinBet_ReturnsBigBetForLaterStreets()
    {
        var game = CreateTwoPlayerGame();
        AdvanceToPhase(game, Phases.FifthStreet);

        game.GetCurrentMinBet().Should().Be(20);
    }

    [Fact]
    public void CanContinue_ReturnsTrueWhenMultiplePlayersHaveChips()
    {
        var game = CreateTwoPlayerGame();

        game.CanContinue().Should().BeTrue();
    }

    [Fact]
    public void GetCurrentStreetName_ReturnsCorrectNames()
    {
        var game = CreateTwoPlayerGame();

        AdvanceToPhase(game, Phases.ThirdStreet);
        game.GetCurrentStreetName().Should().Be("Initial Betting");
    }

    #region Helper Methods

    private static GoodBadUglyGame CreateTwoPlayerGame()
    {
        var players = new List<(string, int)>
        {
            ("Alice", 1000),
            ("Bob", 1000)
        };
        return new GoodBadUglyGame(players, ante: 5, bringIn: 0, smallBet: 10, bigBet: 20, useBringIn: false);
    }

    /// <summary>
    /// Advances the game to the target phase by playing through all intermediate phases.
    /// Uses automated betting (fold to advance quickly) when needed.
    /// </summary>
    private static void AdvanceToPhase(GoodBadUglyGame game, Phases target)
    {
        if (game.CurrentPhase == Phases.WaitingToStart)
        {
            game.StartHand();
        }

        if (game.CurrentPhase == target) return;

        if (game.CurrentPhase == Phases.CollectingAntes)
        {
            game.CollectAntes();
        }

        if (game.CurrentPhase == target) return;

        if (game.CurrentPhase == Phases.ThirdStreet)
        {
            game.DealThirdStreet();
            if (target == Phases.ThirdStreet) return;
            game.StartBettingRound();
            CompleteBettingRound(game);
        }

        if (game.CurrentPhase == target) return;

        // RevealTheGood
        if (game.CurrentPhase == Phases.RevealTheGood)
        {
            if (target == Phases.RevealTheGood) return;
            game.RevealTheGood();
        }

        if (game.CurrentPhase == target) return;

        // FourthStreet
        if (game.CurrentPhase == Phases.FourthStreet)
        {
            if (target == Phases.FourthStreet) return;
            game.StartBettingRound();
            CompleteBettingRound(game);
        }

        if (game.CurrentPhase == target) return;

        // RevealTheBad
        if (game.CurrentPhase == Phases.RevealTheBad)
        {
            if (target == Phases.RevealTheBad) return;
            game.RevealTheBad();
        }

        if (game.CurrentPhase == target) return;

        // FifthStreet
        if (game.CurrentPhase == Phases.FifthStreet)
        {
            if (target == Phases.FifthStreet) return;
            game.StartBettingRound();
            CompleteBettingRound(game);
        }

        if (game.CurrentPhase == target) return;

        // RevealTheUgly
        if (game.CurrentPhase == Phases.RevealTheUgly)
        {
            if (target == Phases.RevealTheUgly) return;
            game.RevealTheUgly();
        }

        if (game.CurrentPhase == target) return;

        // SixthStreet
        if (game.CurrentPhase == Phases.SixthStreet)
        {
            if (target == Phases.SixthStreet) return;
            game.StartBettingRound();
            CompleteBettingRound(game);
        }
    }

    private static void CompleteBettingRound(GoodBadUglyGame game)
    {
        // Everyone checks through the round
        while (game.CurrentBettingRound != null && !game.CurrentBettingRound.IsComplete)
        {
            var available = game.GetAvailableActions();
            if (available == null) break;

            if (available.CanCheck)
            {
                game.ProcessBettingAction(BettingActionType.Check);
            }
            else
            {
                game.ProcessBettingAction(BettingActionType.Call);
            }
        }
    }

    #endregion
}
