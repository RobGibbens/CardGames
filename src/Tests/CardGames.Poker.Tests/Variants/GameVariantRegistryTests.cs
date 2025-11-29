using System;
using CardGames.Poker.Variants;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Variants;

public class GameVariantRegistryTests
{
    [Fact]
    public void RegisterVariant_WithValidData_CanBeRetrievedViaFactory()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var info = new GameVariantInfo("test-variant", "Test Variant");
        GameCreationDelegate factory = (players, sb, bb) => new object();

        // Act
        registry.RegisterVariant(info, factory);
        var gameFactory = new GameVariantFactory(registry);

        // Assert
        gameFactory.IsVariantRegistered("test-variant").Should().BeTrue();
    }

    [Fact]
    public void RegisterVariant_DuplicateId_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var info = new GameVariantInfo("test-variant", "Test Variant");
        GameCreationDelegate factory = (players, sb, bb) => new object();
        registry.RegisterVariant(info, factory);

        // Act & Assert
        var act = () => registry.RegisterVariant(info, factory);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void RegisterVariant_NullInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        GameCreationDelegate factory = (players, sb, bb) => new object();

        // Act & Assert
        var act = () => registry.RegisterVariant(null!, factory);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterVariant_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var info = new GameVariantInfo("test-variant", "Test Variant");

        // Act & Assert
        var act = () => registry.RegisterVariant(info, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetAllVariants_EmptyRegistry_ReturnsEmptyCollection()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var factory = new GameVariantFactory(registry);

        // Act
        var variants = factory.GetAllVariants();

        // Assert
        variants.Should().BeEmpty();
    }

    [Fact]
    public void GetAllVariants_WithRegisteredVariants_ReturnsAllVariants()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        GameCreationDelegate factoryDelegate = (players, sb, bb) => new object();
        registry.RegisterVariant(new GameVariantInfo("variant-1", "Variant 1"), factoryDelegate);
        registry.RegisterVariant(new GameVariantInfo("variant-2", "Variant 2"), factoryDelegate);
        var factory = new GameVariantFactory(registry);

        // Act
        var variants = factory.GetAllVariants();

        // Assert
        variants.Should().HaveCount(2);
        variants.Should().Contain(v => v.Id == "variant-1");
        variants.Should().Contain(v => v.Id == "variant-2");
    }

    [Fact]
    public void IsVariantRegistered_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var info = new GameVariantInfo("test-variant", "Test Variant");
        GameCreationDelegate factoryDelegate = (players, sb, bb) => new object();
        registry.RegisterVariant(info, factoryDelegate);
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.IsVariantRegistered("TEST-VARIANT").Should().BeTrue();
        factory.IsVariantRegistered("Test-Variant").Should().BeTrue();
    }

    [Fact]
    public void IsVariantRegistered_NonExistentVariant_ReturnsFalse()
    {
        // Arrange
        var registry = new GameVariantRegistry();
        var factory = new GameVariantFactory(registry);

        // Act & Assert
        factory.IsVariantRegistered("non-existent").Should().BeFalse();
    }
}
