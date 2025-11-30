using System;
using System.Linq;
using CardGames.Poker.Games;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class DealersChoiceGameTests
{
    private static DealersChoiceGame CreateTestGame(int playerCount = 3, int chipStack = 1000)
    {
        var players = Enumerable.Range(1, playerCount)
            .Select(i => ($"Player{i}", chipStack))
            .ToList();
        return new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);
    }

    [Fact]
    public void Constructor_ValidPlayers_CreatesGame()
    {
        // Arrange & Act
        var game = CreateTestGame();

        // Assert
        game.Players.Should().HaveCount(3);
        game.CurrentPhase.Should().Be(DealersChoicePhase.WaitingToStart);
        game.DealerPosition.Should().Be(0);
        game.AllowWildCards.Should().BeTrue();
    }

    [Fact]
    public void Constructor_TooFewPlayers_ThrowsException()
    {
        // Arrange
        var players = new[] { ("Player1", 1000) };

        // Act
        var act = () => new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Constructor_TooManyPlayers_ThrowsException()
    {
        // Arrange
        var players = Enumerable.Range(1, 11)
            .Select(i => ($"Player{i}", 1000));

        // Act
        var act = () => new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at most 10 players*");
    }

    [Fact]
    public void Constructor_WithoutWildCards_DisablesWildCards()
    {
        // Arrange
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: false);

        // Assert
        game.AllowWildCards.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomVariants_UsesCustomVariants()
    {
        // Arrange
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };
        var variants = new[] { PokerVariant.TexasHoldem, PokerVariant.Omaha };

        // Act
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowedVariants: variants);

        // Assert
        game.AllowedVariants.Should().HaveCount(2);
        game.AllowedVariants.Should().Contain(PokerVariant.TexasHoldem);
        game.AllowedVariants.Should().Contain(PokerVariant.Omaha);
    }

    [Fact]
    public void StartHand_FromWaitingToStart_EntersSelectingVariant()
    {
        // Arrange
        var game = CreateTestGame();

        // Act
        game.StartHand();

        // Assert
        game.CurrentPhase.Should().Be(DealersChoicePhase.SelectingVariant);
    }

    [Fact]
    public void GetAvailableVariants_ReturnsValidVariantsForPlayerCount()
    {
        // Arrange
        var game = CreateTestGame(playerCount: 3);
        game.StartHand();

        // Act
        var variants = game.GetAvailableVariants();

        // Assert
        variants.Should().NotBeEmpty();
        variants.Should().Contain(PokerVariant.TexasHoldem);
        variants.Should().Contain(PokerVariant.Omaha);
    }

    [Fact]
    public void GetAvailableWildCardTypes_WhenWildCardsAllowed_ReturnsAllTypes()
    {
        // Arrange
        var game = CreateTestGame();

        // Act
        var wildCardTypes = game.GetAvailableWildCardTypes();

        // Assert
        wildCardTypes.Should().Contain(WildCardType.None);
        wildCardTypes.Should().Contain(WildCardType.DeucesWild);
        wildCardTypes.Should().Contain(WildCardType.ThreesAndNines);
        wildCardTypes.Should().Contain(WildCardType.KingsWild);
    }

    [Fact]
    public void GetAvailableWildCardTypes_WhenWildCardsDisabled_ReturnsOnlyNone()
    {
        // Arrange
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: false);

        // Act
        var wildCardTypes = game.GetAvailableWildCardTypes();

        // Assert
        wildCardTypes.Should().HaveCount(1);
        wildCardTypes.Should().Contain(WildCardType.None);
    }

    [Fact]
    public void SelectVariant_ValidVariant_MovesToConfigureWildCards()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();

        // Act
        var result = game.SelectVariant(PokerVariant.TexasHoldem);

        // Assert
        result.Success.Should().BeTrue();
        result.SelectedVariant.Should().Be(PokerVariant.TexasHoldem);
        result.RequiresWildCardConfig.Should().BeTrue();
        game.CurrentPhase.Should().Be(DealersChoicePhase.ConfiguringWildCards);
        game.CurrentHandConfig.Should().NotBeNull();
        game.CurrentHandConfig!.Variant.Should().Be(PokerVariant.TexasHoldem);
    }

    [Fact]
    public void SelectVariant_WhenWildCardsDisabled_MovesToPlayingHand()
    {
        // Arrange
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };
        var game = new DealersChoiceGame(players, smallBlind: 5, bigBlind: 10, allowWildCards: false);
        game.StartHand();

        // Act
        var result = game.SelectVariant(PokerVariant.TexasHoldem);

        // Assert
        result.Success.Should().BeTrue();
        result.RequiresWildCardConfig.Should().BeFalse();
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);
        game.CurrentGame.Should().NotBeNull();
    }

    [Fact]
    public void SelectVariant_InvalidPhase_ReturnsError()
    {
        // Arrange
        var game = CreateTestGame(); // Still in WaitingToStart

        // Act
        var result = game.SelectVariant(PokerVariant.TexasHoldem);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("phase");
    }

    [Fact]
    public void SelectVariant_NotAllowedVariant_ReturnsError()
    {
        // Arrange
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };
        var game = new DealersChoiceGame(
            players, 
            smallBlind: 5, 
            bigBlind: 10, 
            allowedVariants: new[] { PokerVariant.TexasHoldem });
        game.StartHand();

        // Act
        var result = game.SelectVariant(PokerVariant.Omaha);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not allowed");
    }

    [Fact]
    public void ConfigureWildCards_ValidConfig_MovesToPlayingHand()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);

        // Act
        var result = game.ConfigureWildCards(WildCardConfiguration.DeucesWild());

        // Assert
        result.Success.Should().BeTrue();
        result.Configuration.Should().NotBeNull();
        result.Configuration!.Enabled.Should().BeTrue();
        result.Configuration.Type.Should().Be(WildCardType.DeucesWild);
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);
        game.CurrentGame.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureWildCards_NoWildCards_MovesToPlayingHand()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);

        // Act
        var result = game.ConfigureWildCards(WildCardConfiguration.None());

        // Assert
        result.Success.Should().BeTrue();
        result.Configuration!.Enabled.Should().BeFalse();
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);
    }

    [Fact]
    public void SkipWildCardConfiguration_MovesToPlayingHand()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);

        // Act
        var result = game.SkipWildCardConfiguration();

        // Assert
        result.Success.Should().BeTrue();
        result.Configuration!.Enabled.Should().BeFalse();
        game.CurrentPhase.Should().Be(DealersChoicePhase.PlayingHand);
    }

    [Fact]
    public void GetCurrentGameAs_ReturnsCorrectType()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.SkipWildCardConfiguration();

        // Act
        var holdemGame = game.GetCurrentGameAs<HoldEmGame>();

        // Assert
        holdemGame.Should().NotBeNull();
        holdemGame!.SmallBlind.Should().Be(5);
        holdemGame.BigBlind.Should().Be(10);
    }

    [Fact]
    public void GetCurrentGameAs_WrongType_ReturnsNull()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.SkipWildCardConfiguration();

        // Act
        var omahaGame = game.GetCurrentGameAs<OmahaGame>();

        // Assert
        omahaGame.Should().BeNull();
    }

    [Fact]
    public void CompleteHand_AdvancesDealerAndPhase()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.SkipWildCardConfiguration();

        // Act
        game.CompleteHand();

        // Assert
        game.CurrentPhase.Should().Be(DealersChoicePhase.HandComplete);
        game.DealerPosition.Should().Be(1); // Moved from 0 to 1
        game.HandsPlayed.Should().Be(1);
    }

    [Fact]
    public void CurrentDealer_ReturnsCorrectPlayer()
    {
        // Arrange
        var game = CreateTestGame();

        // Act & Assert
        game.CurrentDealer.Player.Name.Should().Be("Player1");
        
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.SkipWildCardConfiguration();
        game.CompleteHand();

        game.CurrentDealer.Player.Name.Should().Be("Player2");
    }

    [Fact]
    public void GetWildCardValues_ReturnsCorrectValues()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        game.ConfigureWildCards(WildCardConfiguration.DeucesWild());

        // Act
        var wildValues = game.GetWildCardValues();

        // Assert
        wildValues.Should().Contain(2);
    }

    [Fact]
    public void GetWildCardValues_ThreesAndNines_ReturnsBothValues()
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();
        game.SelectVariant(PokerVariant.TexasHoldem);
        var config = new WildCardConfiguration { Enabled = true, Type = WildCardType.ThreesAndNines };
        game.ConfigureWildCards(config);

        // Act
        var wildValues = game.GetWildCardValues();

        // Assert
        wildValues.Should().Contain(3);
        wildValues.Should().Contain(9);
    }

    [Fact]
    public void CanContinue_WithEnoughPlayers_ReturnsTrue()
    {
        // Arrange
        var game = CreateTestGame();

        // Act & Assert
        game.CanContinue().Should().BeTrue();
    }

    [Fact]
    public void GetPlayersWithChips_ReturnsPlayersWithChips()
    {
        // Arrange
        var game = CreateTestGame();

        // Act
        var playersWithChips = game.GetPlayersWithChips();

        // Assert
        playersWithChips.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    [InlineData(PokerVariant.FiveCardDraw)]
    public void SelectVariant_CreatesCorrectGameType(PokerVariant variant)
    {
        // Arrange
        var game = CreateTestGame();
        game.StartHand();

        // Act
        game.SelectVariant(variant);
        game.SkipWildCardConfiguration();

        // Assert
        game.CurrentGame.Should().NotBeNull();
        game.CurrentHandConfig!.Variant.Should().Be(variant);
    }
}
