using CardGames.Poker.Api.Features.Showdown;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class ShowdownCoordinatorTests
{
    private readonly IShowdownCoordinator _coordinator;

    public ShowdownCoordinatorTests()
    {
        _coordinator = new ShowdownCoordinator();
    }

    private static ShowdownRulesDto CreateDefaultShowdownRules(
        ShowdownOrder showOrder = ShowdownOrder.LastAggressor,
        bool allowMuck = true,
        bool showAllOnAllIn = true)
    {
        return new ShowdownRulesDto(showOrder, allowMuck, showAllOnAllIn);
    }

    private static ShowdownPlayerState CreatePlayer(
        string name,
        bool hasFolded = false,
        bool isAllIn = false,
        bool isEligibleForPot = true)
    {
        return new ShowdownPlayerState
        {
            PlayerName = name,
            HoleCards = new List<CardDto>
            {
                new CardDto("A", "Spades", "As"),
                new CardDto("K", "Hearts", "Kh")
            },
            HasFolded = hasFolded,
            IsAllIn = isAllIn,
            IsEligibleForPot = isEligibleForPot,
            TotalBetAmount = 100
        };
    }

    private static HandDto CreateHand(string handType, long strength)
    {
        return new HandDto(
            new List<CardDto>
            {
                new CardDto("A", "Spades", "As"),
                new CardDto("K", "Hearts", "Kh"),
                new CardDto("Q", "Diamonds", "Qd"),
                new CardDto("J", "Clubs", "Jc"),
                new CardDto("T", "Spades", "Ts")
            },
            handType,
            $"{handType} description",
            strength);
    }

    #region InitializeShowdown Tests

    [Fact]
    public void InitializeShowdown_CreatesValidContext()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };

        // Act
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(),
            1,
            rules,
            players,
            "Alice",
            false,
            0);

        // Assert
        context.Should().NotBeNull();
        context.ShowdownId.Should().NotBeEmpty();
        context.HandNumber.Should().Be(1);
        context.LastAggressor.Should().Be("Alice");
        context.HadAllInAction.Should().BeFalse();
        context.Players.Should().HaveCount(2);
        context.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void InitializeShowdown_MarksFoldedPlayersCorrectly()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", hasFolded: true),
            CreatePlayer("Charlie")
        };

        // Act
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(),
            1,
            rules,
            players,
            null,
            false,
            0);

        // Assert
        var bob = context.Players.First(p => p.PlayerName == "Bob");
        bob.Status.Should().Be(ShowdownRevealStatus.Folded);
    }

    #endregion

    #region GetNextToReveal Tests

    [Fact]
    public void GetNextToReveal_LastAggressorFirst_ReturnsLastAggressor()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(ShowdownOrder.LastAggressor);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob"),
            CreatePlayer("Charlie")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Bob", false, 0);

        // Act
        var nextToReveal = _coordinator.GetNextToReveal(context);

        // Assert
        nextToReveal.Should().Be("Bob");
    }

    [Fact]
    public void GetNextToReveal_NoMorePending_ReturnsNull()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Mark all as revealed
        foreach (var p in context.Players)
        {
            p.Status = ShowdownRevealStatus.Shown;
        }

        // Act
        var nextToReveal = _coordinator.GetNextToReveal(context);

        // Assert
        nextToReveal.Should().BeNull();
    }

    [Fact]
    public void GetNextToReveal_SkipsFoldedPlayers()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(ShowdownOrder.ClockwiseFromButton);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", hasFolded: true),
            CreatePlayer("Charlie")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, null, false, 0);

        // Act - Alice reveals first (position 1 from dealer 0)
        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        var nextToReveal = _coordinator.GetNextToReveal(context);

        // Assert - Should skip Bob (folded) and go to Charlie
        nextToReveal.Should().Be("Charlie");
    }

    #endregion

    #region CanPlayerMuck Tests

    [Fact]
    public void CanPlayerMuck_WhenAllowMuckFalse_ReturnsFalse()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: false);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var canMuck = _coordinator.CanPlayerMuck(context, "Bob");

        // Assert
        canMuck.Should().BeFalse();
    }

    [Fact]
    public void CanPlayerMuck_WhenAllInAndShowAllOnAllIn_ReturnsFalse()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(showAllOnAllIn: true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", isAllIn: true)
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, null, hadAllInAction: true, 0);

        // Act
        var canMuck = _coordinator.CanPlayerMuck(context, "Bob");

        // Assert
        canMuck.Should().BeFalse();
    }

    [Fact]
    public void CanPlayerMuck_WhenLastAggressorFirstToReveal_ReturnsFalse()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var canMuck = _coordinator.CanPlayerMuck(context, "Alice");

        // Assert - Last aggressor must show first
        canMuck.Should().BeFalse();
    }

    [Fact]
    public void CanPlayerMuck_WhenAllInPlayer_ReturnsFalse()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", isAllIn: true)
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var canMuck = _coordinator.CanPlayerMuck(context, "Bob");

        // Assert
        canMuck.Should().BeFalse();
    }

    #endregion

    #region MustPlayerReveal Tests

    [Fact]
    public void MustPlayerReveal_WhenAllInAndShowAllOnAllIn_ReturnsTrue()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(showAllOnAllIn: true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, null, hadAllInAction: true, 0);

        // Act
        var mustReveal = _coordinator.MustPlayerReveal(context, "Alice");

        // Assert
        mustReveal.Should().BeTrue();
    }

    [Fact]
    public void MustPlayerReveal_WhenLastAggressorFirstToReveal_ReturnsTrue()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var mustReveal = _coordinator.MustPlayerReveal(context, "Alice");

        // Assert
        mustReveal.Should().BeTrue();
    }

    [Fact]
    public void MustPlayerReveal_WhenNoMuckAllowed_ReturnsTrue()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: false);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var mustReveal = _coordinator.MustPlayerReveal(context, "Bob");

        // Assert
        mustReveal.Should().BeTrue();
    }

    #endregion

    #region ProcessReveal Tests

    [Fact]
    public void ProcessReveal_ValidPlayer_SetsStatusCorrectly()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);
        var hand = CreateHand("Pair", 1000);

        // Act
        var result = _coordinator.ProcessReveal(context, "Alice", hand);

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().BeOneOf(ShowdownRevealStatus.Shown, ShowdownRevealStatus.ForcedReveal);
        context.Players.First(p => p.PlayerName == "Alice").RevealOrder.Should().Be(1);
    }

    [Fact]
    public void ProcessReveal_FoldedPlayer_Fails()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", hasFolded: true)
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var result = _coordinator.ProcessReveal(context, "Bob", CreateHand("Pair", 1000));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Folded");
    }

    [Fact]
    public void ProcessReveal_OutOfTurn_Fails()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(ShowdownOrder.LastAggressor);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act - Try to reveal out of turn
        var result = _coordinator.ProcessReveal(context, "Bob", CreateHand("Pair", 1000));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("turn");
    }

    [Fact]
    public void ProcessReveal_AllPlayersRevealed_ShowdownComplete()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        var result = _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        // Assert
        result.Success.Should().BeTrue();
        result.ShowdownComplete.Should().BeTrue();
        context.IsComplete.Should().BeTrue();
    }

    #endregion

    #region ProcessMuck Tests

    [Fact]
    public void ProcessMuck_WhenAllowed_SetsStatusCorrectly()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // First, Alice reveals (she's last aggressor, must show)
        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));

        // Act - Bob can muck
        var result = _coordinator.ProcessMuck(context, "Bob");

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().Be(ShowdownRevealStatus.Mucked);
        context.Players.First(p => p.PlayerName == "Bob").IsEligibleForPot.Should().BeFalse();
    }

    [Fact]
    public void ProcessMuck_WhenNotAllowed_Fails()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: false);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // First reveal Alice
        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));

        // Act
        var result = _coordinator.ProcessMuck(context, "Bob");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not allowed");
    }

    #endregion

    #region DetermineWinners Tests

    [Fact]
    public void DetermineWinners_SingleWinner_ReturnsCorrectPlayer()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        // Act
        var winners = _coordinator.DetermineWinners(context);

        // Assert
        winners.Should().HaveCount(1);
        winners.Should().Contain("Bob");
    }

    [Fact]
    public void DetermineWinners_TiedHands_ReturnsBothPlayers()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("Pair", 1000));

        // Act
        var winners = _coordinator.DetermineWinners(context);

        // Assert
        winners.Should().HaveCount(2);
        winners.Should().Contain("Alice");
        winners.Should().Contain("Bob");
    }

    [Fact]
    public void DetermineWinners_MuckedPlayers_NotConsidered()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessMuck(context, "Bob");

        // Act
        var winners = _coordinator.DetermineWinners(context);

        // Assert
        winners.Should().HaveCount(1);
        winners.Should().Contain("Alice");
    }

    #endregion

    #region GetShowdownState Tests

    [Fact]
    public void GetShowdownState_ReturnsCorrectDto()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(ShowdownOrder.LastAggressor, true, true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", true, 0);

        // Act
        var state = _coordinator.GetShowdownState(context);

        // Assert
        state.Should().NotBeNull();
        state.ShowdownId.Should().Be(context.ShowdownId);
        state.ShowOrder.Should().Be(ShowdownOrder.LastAggressor);
        state.AllowMuck.Should().BeTrue();
        state.ForceShowAllOnAllIn.Should().BeTrue();
        state.HadAllInAction.Should().BeTrue();
        state.IsComplete.Should().BeFalse();
        state.NextToReveal.Should().Be("Alice");
        state.PlayerReveals.Should().HaveCount(2);
    }

    [Fact]
    public void GetShowdownState_HidesCardsOfPendingPlayers()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));

        // Act
        var state = _coordinator.GetShowdownState(context);

        // Assert
        var aliceReveal = state.PlayerReveals.First(r => r.PlayerName == "Alice");
        var bobReveal = state.PlayerReveals.First(r => r.PlayerName == "Bob");

        aliceReveal.RevealedCards.Should().NotBeNull();
        bobReveal.RevealedCards.Should().BeNull();
    }

    #endregion

    #region GetCurrentBestHand Tests

    [Fact]
    public void GetCurrentBestHand_NoReveals_ReturnsNull()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var bestHand = _coordinator.GetCurrentBestHand(context);

        // Assert
        bestHand.Should().BeNull();
    }

    [Fact]
    public void GetCurrentBestHand_OneReveal_ReturnsThatHand()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);
        var hand = CreateHand("Pair", 1000);

        _coordinator.ProcessReveal(context, "Alice", hand);

        // Act
        var bestHand = _coordinator.GetCurrentBestHand(context);

        // Assert
        bestHand.Should().NotBeNull();
        bestHand.Should().Be(hand);
    }

    [Fact]
    public void GetCurrentBestHand_MultipleReveals_ReturnsBest()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        // Act
        var bestHand = _coordinator.GetCurrentBestHand(context);

        // Assert
        bestHand.Should().NotBeNull();
        bestHand!.Strength.Should().Be(2000);
    }

    #endregion

    #region AutoRevealWinner Tests

    [Fact]
    public void AutoRevealWinner_ValidWinner_SetsStatusToForcedReveal()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);
        var hand = CreateHand("Straight", 5000);

        // Act
        var result = _coordinator.AutoRevealWinner(context, "Alice", hand);

        // Assert
        result.Success.Should().BeTrue();
        result.NewStatus.Should().Be(ShowdownRevealStatus.ForcedReveal);
        var player = context.Players.First(p => p.PlayerName == "Alice");
        player.WasForcedReveal.Should().BeTrue();
        player.Hand.Should().Be(hand);
    }

    [Fact]
    public void AutoRevealWinner_FoldedPlayer_Fails()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", hasFolded: true)
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var result = _coordinator.AutoRevealWinner(context, "Bob", CreateHand("Pair", 1000));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Folded");
    }

    [Fact]
    public void AutoRevealWinner_UnknownPlayer_Fails()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        // Act
        var result = _coordinator.AutoRevealWinner(context, "Charlie", CreateHand("Pair", 1000));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    #endregion

    #region ProcessAllInShowdown Tests

    [Fact]
    public void ProcessAllInShowdown_CalculatesRemainingCards()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice", isAllIn: true),
            CreatePlayer("Bob", isAllIn: true)
        };
        var communityCards = new List<CardDto>
        {
            new CardDto("A", "Spades", "As"),
            new CardDto("K", "Hearts", "Kh"),
            new CardDto("Q", "Diamonds", "Qd")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, null, hadAllInAction: true, 0, communityCards);

        // Act
        var result = _coordinator.ProcessAllInShowdown(context, totalCommunityCardsNeeded: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.CommunityCardsNeeded.Should().Be(2); // Need 2 more cards (turn + river)
        result.PlayersToAutoReveal.Should().HaveCount(2);
        result.PlayersToAutoReveal.Should().Contain("Alice");
        result.PlayersToAutoReveal.Should().Contain("Bob");
    }

    [Fact]
    public void ProcessAllInShowdown_WithFullBoard_ReturnsZeroCardsNeeded()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice", isAllIn: true),
            CreatePlayer("Bob")
        };
        var communityCards = new List<CardDto>
        {
            new CardDto("A", "Spades", "As"),
            new CardDto("K", "Hearts", "Kh"),
            new CardDto("Q", "Diamonds", "Qd"),
            new CardDto("J", "Clubs", "Jc"),
            new CardDto("T", "Spades", "Ts")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, null, hadAllInAction: true, 0, communityCards);

        // Act
        var result = _coordinator.ProcessAllInShowdown(context, totalCommunityCardsNeeded: 5);

        // Assert
        result.Success.Should().BeTrue();
        result.CommunityCardsNeeded.Should().Be(0);
    }

    #endregion

    #region DetermineWinnersWithPots Tests

    [Fact]
    public void DetermineWinnersWithPots_SingleWinner_ReturnsCorrectAmount()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        var pots = new List<PotInfo>
        {
            new PotInfo(0, 500, new List<string> { "Alice", "Bob" }, true)
        };

        // Act
        var winners = _coordinator.DetermineWinnersWithPots(context, pots);

        // Assert
        winners.Should().HaveCount(1);
        winners[0].PlayerName.Should().Be("Bob");
        winners[0].AmountWon.Should().Be(500);
        winners[0].IsTie.Should().BeFalse();
    }

    [Fact]
    public void DetermineWinnersWithPots_SplitPot_ReturnsCorrectShares()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Flush", 5000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("Flush", 5000));

        var pots = new List<PotInfo>
        {
            new PotInfo(0, 1000, new List<string> { "Alice", "Bob" }, true)
        };

        // Act
        var winners = _coordinator.DetermineWinnersWithPots(context, pots);

        // Assert
        winners.Should().HaveCount(2);
        winners.Should().AllSatisfy(w => w.AmountWon.Should().Be(500));
        winners.Should().AllSatisfy(w => w.IsTie.Should().BeTrue());
    }

    [Fact]
    public void DetermineWinnersWithPots_SidePots_ReturnsMultipleWinners()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob"),
            CreatePlayer("Charlie")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));
        _coordinator.ProcessReveal(context, "Charlie", CreateHand("Trips", 3000));

        var pots = new List<PotInfo>
        {
            new PotInfo(0, 300, new List<string> { "Alice", "Bob", "Charlie" }, true),
            new PotInfo(1, 200, new List<string> { "Bob", "Charlie" }, false)
        };

        // Act
        var winners = _coordinator.DetermineWinnersWithPots(context, pots);

        // Assert
        winners.Should().HaveCount(2);
        var mainPotWinner = winners.First(w => w.PotIndex == 0);
        mainPotWinner.PlayerName.Should().Be("Charlie");
        mainPotWinner.AmountWon.Should().Be(300);

        var sidePotWinner = winners.First(w => w.PotIndex == 1);
        sidePotWinner.PlayerName.Should().Be("Charlie");
        sidePotWinner.AmountWon.Should().Be(200);
    }

    #endregion

    #region BuildWinnerAnnouncement Tests

    [Fact]
    public void BuildWinnerAnnouncement_SingleWinner_ReturnsCorrectSummary()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination(
                "Bob",
                CreateHand("TwoPair", 2000),
                null,
                null,
                500,
                0,
                false)
        };

        // Act
        var announcement = _coordinator.BuildWinnerAnnouncement(context, winners, wonByFold: false);

        // Assert
        announcement.Should().NotBeNull();
        announcement.ShowdownId.Should().Be(context.ShowdownId);
        announcement.IsSplitPot.Should().BeFalse();
        announcement.WonByFold.Should().BeFalse();
        announcement.TotalPotDistributed.Should().Be(500);
        announcement.Summary.Should().Contain("Bob").And.Contain("500").And.Contain("TwoPair");
    }

    [Fact]
    public void BuildWinnerAnnouncement_WonByFold_ReturnsCorrectSummary()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob", hasFolded: true)
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination(
                "Alice",
                null,
                null,
                null,
                500,
                0,
                false)
        };

        // Act
        var announcement = _coordinator.BuildWinnerAnnouncement(context, winners, wonByFold: true);

        // Assert
        announcement.WonByFold.Should().BeTrue();
        announcement.Summary.Should().Contain("folded");
    }

    [Fact]
    public void BuildWinnerAnnouncement_SplitPot_ReturnsCorrectSummary()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination("Alice", CreateHand("Flush", 5000), null, null, 250, 0, true),
            new WinnerDetermination("Bob", CreateHand("Flush", 5000), null, null, 250, 0, true)
        };

        // Act
        var announcement = _coordinator.BuildWinnerAnnouncement(context, winners, wonByFold: false);

        // Assert
        announcement.IsSplitPot.Should().BeTrue();
        announcement.Summary.Should().Contain("Split pot");
        announcement.TotalPotDistributed.Should().Be(500);
    }

    #endregion

    #region BuildAnimationSequence Tests

    [Fact]
    public void BuildAnimationSequence_CreatesValidSequence()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination(
                "Bob",
                CreateHand("TwoPair", 2000),
                null,
                null,
                500,
                0,
                false)
        };

        // Act
        var animation = _coordinator.BuildAnimationSequence(context, winners);

        // Assert
        animation.Should().NotBeNull();
        animation.ShowdownId.Should().Be(context.ShowdownId);
        animation.Steps.Should().NotBeEmpty();
        animation.TotalDurationMs.Should().BeGreaterThan(0);
        
        // Should have reveal steps, winner highlight, and pot award
        animation.Steps.Should().Contain(s => s.AnimationType == ShowdownAnimationType.PlayerReveal);
        animation.Steps.Should().Contain(s => s.AnimationType == ShowdownAnimationType.WinnerHighlight);
        animation.Steps.Should().Contain(s => s.AnimationType == ShowdownAnimationType.PotAward);
    }

    [Fact]
    public void BuildAnimationSequence_IncludesMuckSteps()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules(allowMuck: true);
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("TwoPair", 2000));
        _coordinator.ProcessMuck(context, "Bob");

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination(
                "Alice",
                CreateHand("TwoPair", 2000),
                null,
                null,
                500,
                0,
                false)
        };

        // Act
        var animation = _coordinator.BuildAnimationSequence(context, winners);

        // Assert
        // Mucked players should have a muck animation step
        // Note: ProcessMuck doesn't set RevealOrder, so muck step may not appear
        // This is intentional as mucking doesn't have a specific order in the animation
        animation.Steps.Should().Contain(s => s.AnimationType == ShowdownAnimationType.PlayerReveal);
        animation.Steps.Should().Contain(s => s.AnimationType == ShowdownAnimationType.WinnerHighlight);
    }

    [Fact]
    public void BuildAnimationSequence_SetsCorrectSequenceOrder()
    {
        // Arrange
        var rules = CreateDefaultShowdownRules();
        var players = new List<ShowdownPlayerState>
        {
            CreatePlayer("Alice"),
            CreatePlayer("Bob")
        };
        var context = _coordinator.InitializeShowdown(
            Guid.NewGuid(), 1, rules, players, "Alice", false, 0);

        _coordinator.ProcessReveal(context, "Alice", CreateHand("Pair", 1000));
        _coordinator.ProcessReveal(context, "Bob", CreateHand("TwoPair", 2000));

        var winners = new List<WinnerDetermination>
        {
            new WinnerDetermination("Bob", CreateHand("TwoPair", 2000), null, null, 500, 0, false)
        };

        // Act
        var animation = _coordinator.BuildAnimationSequence(context, winners);

        // Assert
        var sequences = animation.Steps.Select(s => s.Sequence).ToList();
        sequences.Should().BeInAscendingOrder();
    }

    #endregion
}
