using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Web.Tests.Services;

public class VariantDisplayServiceTests
{
    private readonly VariantDisplayService _service = new();

    [Fact]
    public void GetDisplayConfig_TexasHoldem_ReturnsValidConfig()
    {
        // Act
        var config = _service.GetDisplayConfig(PokerVariant.TexasHoldem);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.TexasHoldem);
        config.HoleCardCount.Should().Be(2);
        config.HasCommunityCards.Should().BeTrue();
    }

    [Fact]
    public void GetDisplayConfig_UnknownVariant_ReturnsNull()
    {
        // Act
        var config = _service.GetDisplayConfig(PokerVariant.DealersChoice);

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public void GetDisplayConfig_CachesResults()
    {
        // Act
        var config1 = _service.GetDisplayConfig(PokerVariant.Omaha);
        var config2 = _service.GetDisplayConfig(PokerVariant.Omaha);

        // Assert
        config1.Should().BeSameAs(config2);
    }

    [Fact]
    public void GetAllDisplayConfigs_ReturnsAllSupportedVariants()
    {
        // Act
        var configs = _service.GetAllDisplayConfigs();

        // Assert
        configs.Should().NotBeEmpty();
        configs.Should().Contain(c => c.Variant == PokerVariant.TexasHoldem);
        configs.Should().Contain(c => c.Variant == PokerVariant.Omaha);
        configs.Should().Contain(c => c.Variant == PokerVariant.SevenCardStud);
        configs.Should().Contain(c => c.Variant == PokerVariant.FiveCardDraw);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, 2)]
    [InlineData(PokerVariant.Omaha, 4)]
    [InlineData(PokerVariant.SevenCardStud, 7)]
    [InlineData(PokerVariant.FiveCardDraw, 5)]
    public void GetHoleCardCount_ReturnsCorrectCount(PokerVariant variant, int expectedCount)
    {
        // Act
        var count = _service.GetHoleCardCount(variant);

        // Assert
        count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, 5)]
    [InlineData(PokerVariant.Omaha, 5)]
    [InlineData(PokerVariant.SevenCardStud, 0)]
    [InlineData(PokerVariant.FiveCardDraw, 0)]
    public void GetCommunityCardCount_ReturnsCorrectCount(PokerVariant variant, int expectedCount)
    {
        // Act
        var count = _service.GetCommunityCardCount(variant);

        // Assert
        count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, true)]
    [InlineData(PokerVariant.Omaha, true)]
    [InlineData(PokerVariant.SevenCardStud, false)]
    [InlineData(PokerVariant.FiveCardDraw, false)]
    public void ShowCommunityCards_ReturnsCorrectValue(PokerVariant variant, bool expected)
    {
        // Act
        var result = _service.ShowCommunityCards(variant);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, false)]
    [InlineData(PokerVariant.SevenCardStud, true)]
    [InlineData(PokerVariant.Baseball, true)]
    [InlineData(PokerVariant.FollowTheQueen, true)]
    public void IsStudGame_ReturnsCorrectValue(PokerVariant variant, bool expected)
    {
        // Act
        var result = _service.IsStudGame(variant);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetFaceUpCardIndices_StudGame_ReturnsIndices()
    {
        // Act
        var indices = _service.GetFaceUpCardIndices(PokerVariant.SevenCardStud);

        // Assert
        indices.Should().NotBeEmpty();
        indices.Should().BeEquivalentTo(new[] { 2, 3, 4, 5 });
    }

    [Fact]
    public void GetFaceUpCardIndices_NonStudGame_ReturnsEmpty()
    {
        // Act
        var indices = _service.GetFaceUpCardIndices(PokerVariant.TexasHoldem);

        // Assert
        indices.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PokerVariant.FiveCardDraw, true)]
    [InlineData(PokerVariant.TexasHoldem, false)]
    [InlineData(PokerVariant.SevenCardStud, false)]
    public void AllowsDraw_ReturnsCorrectValue(PokerVariant variant, bool expected)
    {
        // Act
        var result = _service.AllowsDraw(variant);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetMaxDrawCount_FiveCardDraw_Returns3()
    {
        // Act
        var count = _service.GetMaxDrawCount(PokerVariant.FiveCardDraw);

        // Assert
        count.Should().Be(3);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, "large")]
    [InlineData(PokerVariant.Omaha, "medium")]
    [InlineData(PokerVariant.SevenCardStud, "small")]
    public void GetRecommendedCardSize_ReturnsCorrectSize(PokerVariant variant, string expectedSize)
    {
        // Act
        var size = _service.GetRecommendedCardSize(variant);

        // Assert
        size.Should().Be(expectedSize);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, "normal")]
    [InlineData(PokerVariant.Omaha, "stacked")]
    [InlineData(PokerVariant.SevenCardStud, "stud")]
    public void GetRecommendedOpponentLayout_ReturnsCorrectLayout(PokerVariant variant, string expectedLayout)
    {
        // Act
        var layout = _service.GetRecommendedOpponentLayout(variant);

        // Assert
        layout.Should().Be(expectedLayout);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, VariantDisplayType.CommunityCards)]
    [InlineData(PokerVariant.SevenCardStud, VariantDisplayType.Stud)]
    [InlineData(PokerVariant.FiveCardDraw, VariantDisplayType.Draw)]
    public void GetDisplayType_ReturnsCorrectType(PokerVariant variant, VariantDisplayType expectedType)
    {
        // Act
        var type = _service.GetDisplayType(variant);

        // Assert
        type.Should().Be(expectedType);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, "NL")]
    [InlineData(PokerVariant.Omaha, "PL")]
    [InlineData(PokerVariant.SevenCardStud, "FL")]
    public void GetLimitTypeAbbreviation_ReturnsCorrectAbbreviation(PokerVariant variant, string expectedAbbr)
    {
        // Act
        var abbr = _service.GetLimitTypeAbbreviation(variant);

        // Assert
        abbr.Should().Be(expectedAbbr);
    }

    [Fact]
    public void GetBettingRoundNames_TexasHoldem_ReturnsCorrectNames()
    {
        // Act
        var names = _service.GetBettingRoundNames(PokerVariant.TexasHoldem);

        // Assert
        names.Should().HaveCount(4);
        names.Should().ContainInOrder("Preflop", "Flop", "Turn", "River");
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, 2)]
    [InlineData(PokerVariant.Omaha, 4)]
    [InlineData(PokerVariant.SevenCardStud, 3)]
    public void GetOpponentFaceDownCount_ReturnsCorrectCount(PokerVariant variant, int expected)
    {
        // Act
        var count = _service.GetOpponentFaceDownCount(variant);

        // Assert
        count.Should().Be(expected);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem, 0)]
    [InlineData(PokerVariant.SevenCardStud, 4)]
    public void GetOpponentFaceUpCount_ReturnsCorrectCount(PokerVariant variant, int expected)
    {
        // Act
        var count = _service.GetOpponentFaceUpCount(variant);

        // Assert
        count.Should().Be(expected);
    }
}
