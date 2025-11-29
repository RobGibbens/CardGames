using System;
using CardGames.Poker.Games;
using CardGames.Poker.Variants;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Variants;

public class GameVariantFactoryTests
{
    private static GameVariantRegistry CreateRegistryWithHoldEmAndOmaha()
    {
        var registry = new GameVariantRegistry();
        
        var holdemInfo = new GameVariantInfo(
            "texas-holdem", 
            "Texas Hold'em", 
            "Texas Hold'em poker");
        registry.RegisterVariant(holdemInfo, (players, sb, bb) => new HoldEmGame(players, sb, bb));
        
        var omahaInfo = new GameVariantInfo(
            "omaha", 
            "Omaha", 
            "Omaha poker");
        registry.RegisterVariant(omahaInfo, (players, sb, bb) => new OmahaGame(players, sb, bb));
        
        return registry;
    }

    [Fact]
    public void CreateGame_TexasHoldem_ReturnsHoldEmGame()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act
        var game = factory.CreateGame("texas-holdem", players, 5, 10);

        // Assert
        game.Should().BeOfType<HoldEmGame>();
        var holdemGame = (HoldEmGame)game;
        holdemGame.SmallBlind.Should().Be(5);
        holdemGame.BigBlind.Should().Be(10);
        holdemGame.Players.Should().HaveCount(2);
    }

    [Fact]
    public void CreateGame_Omaha_ReturnsOmahaGame()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act
        var game = factory.CreateGame("omaha", players, 10, 20);

        // Assert
        game.Should().BeOfType<OmahaGame>();
        var omahaGame = (OmahaGame)game;
        omahaGame.SmallBlind.Should().Be(10);
        omahaGame.BigBlind.Should().Be(20);
        omahaGame.Players.Should().HaveCount(2);
    }

    [Fact]
    public void CreateGame_CaseInsensitiveVariantId_Works()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act
        var game = factory.CreateGame("TEXAS-HOLDEM", players, 5, 10);

        // Assert
        game.Should().BeOfType<HoldEmGame>();
    }

    [Fact]
    public void CreateGame_UnregisteredVariant_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act & Assert
        var act = () => factory.CreateGame("unknown-variant", players, 5, 10);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*not registered*");
    }

    [Fact]
    public void CreateGame_NullVariantId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act & Assert
        var act = () => factory.CreateGame(null!, players, 5, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateGame_EmptyVariantId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);
        var players = new[] { ("Player1", 1000), ("Player2", 1000) };

        // Act & Assert
        var act = () => factory.CreateGame("", players, 5, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsVariantRegistered_RegisteredVariant_ReturnsTrue()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.IsVariantRegistered("texas-holdem").Should().BeTrue();
        factory.IsVariantRegistered("omaha").Should().BeTrue();
    }

    [Fact]
    public void IsVariantRegistered_UnregisteredVariant_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.IsVariantRegistered("unknown").Should().BeFalse();
    }

    [Fact]
    public void IsVariantRegistered_NullOrEmpty_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.IsVariantRegistered(null!).Should().BeFalse();
        factory.IsVariantRegistered("").Should().BeFalse();
        factory.IsVariantRegistered("  ").Should().BeFalse();
    }

    [Fact]
    public void GetAllVariants_ReturnsAllRegisteredVariants()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act
        var variants = factory.GetAllVariants();

        // Assert
        variants.Should().HaveCount(2);
        variants.Should().Contain(v => v.Id == "texas-holdem");
        variants.Should().Contain(v => v.Id == "omaha");
    }

    [Fact]
    public void GetVariant_ExistingVariant_ReturnsVariantInfo()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act
        var variant = factory.GetVariant("texas-holdem");

        // Assert
        variant.Should().NotBeNull();
        variant!.Id.Should().Be("texas-holdem");
        variant.Name.Should().Be("Texas Hold'em");
    }

    [Fact]
    public void GetVariant_NonExistentVariant_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act
        var variant = factory.GetVariant("unknown");

        // Assert
        variant.Should().BeNull();
    }

    [Fact]
    public void GetVariant_NullOrEmpty_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistryWithHoldEmAndOmaha();
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.GetVariant(null!).Should().BeNull();
        factory.GetVariant("").Should().BeNull();
        factory.GetVariant("  ").Should().BeNull();
    }

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GameVariantFactory(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
