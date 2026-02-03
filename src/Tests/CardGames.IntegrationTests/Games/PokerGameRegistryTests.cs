using CardGames.Poker.Api.Games;

namespace CardGames.IntegrationTests.Games;

/// <summary>
/// Integration tests for the Poker game registries.
/// </summary>
public class PokerGameRegistryTests
{
    #region PokerGameMetadataRegistry Tests

    [Theory]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode, "Five Card Draw")]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode, "Seven Card Stud")]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode, "Kings and Lows")]
    [InlineData(PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, "Twos, Jacks, Man with the Axe")]
    [InlineData(PokerGameMetadataRegistry.HoldEmCode, "Texas Hold 'Em")]
    public void TryGet_KnownGameCode_ReturnsMetadata(string gameCode, string expectedName)
    {
        // Act
        var result = PokerGameMetadataRegistry.TryGet(gameCode, out var metadata);

        // Assert
        result.Should().BeTrue();
        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be(expectedName);
    }

    [Fact]
    public void TryGet_UnknownGameCode_ReturnsFalse()
    {
        // Act
        var result = PokerGameMetadataRegistry.TryGet("UNKNOWN", out var metadata);

        // Assert
        result.Should().BeFalse();
        metadata.Should().BeNull();
    }

    [Fact]
    public void TryGet_NullGameCode_ReturnsFalse()
    {
        // Act
        var result = PokerGameMetadataRegistry.TryGet(null, out var metadata);

        // Assert
        result.Should().BeFalse();
        metadata.Should().BeNull();
    }

    [Fact]
    public void TryGet_CaseInsensitive()
    {
        // Act
        var result1 = PokerGameMetadataRegistry.TryGet("fivecarddraw", out var metadata1);
        var result2 = PokerGameMetadataRegistry.TryGet("FIVECARDDRAW", out var metadata2);
        var result3 = PokerGameMetadataRegistry.TryGet("FiveCardDraw", out var metadata3);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
        metadata1!.Name.Should().Be(metadata2!.Name);
        metadata2.Name.Should().Be(metadata3!.Name);
    }

    [Theory]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode, true)]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode, false)]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode, false)]
    [InlineData(PokerGameMetadataRegistry.HoldEmCode, false)]
    public void IsSevenCardStud_ReturnsCorrectValue(string gameCode, bool expected)
    {
        // Act
        var result = PokerGameMetadataRegistry.IsSevenCardStud(gameCode);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsSevenCardStud_NullCode_ReturnsFalse()
    {
        // Act
        var result = PokerGameMetadataRegistry.IsSevenCardStud(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FiveCardDraw_HasDrawPhase()
    {
        // Act
        PokerGameMetadataRegistry.TryGet(PokerGameMetadataRegistry.FiveCardDrawCode, out var metadata);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.HasDrawPhase.Should().BeTrue();
    }

    [Fact]
    public void SevenCardStud_NoDrawPhase()
    {
        // Act
        PokerGameMetadataRegistry.TryGet(PokerGameMetadataRegistry.SevenCardStudCode, out var metadata);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.HasDrawPhase.Should().BeFalse();
    }

    [Fact]
    public void FiveCardDraw_Has5HoleCards()
    {
        // Act
        PokerGameMetadataRegistry.TryGet(PokerGameMetadataRegistry.FiveCardDrawCode, out var metadata);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.InitialHoleCards.Should().Be(5);
    }

    [Fact]
    public void SevenCardStud_Has2InitialHoleCards()
    {
        // Act
        PokerGameMetadataRegistry.TryGet(PokerGameMetadataRegistry.SevenCardStudCode, out var metadata);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.InitialHoleCards.Should().Be(2);
        metadata.InitialBoardCards.Should().Be(1);
    }

    [Fact]
    public void HoldEm_Has2HoleCards()
    {
        // Act
        PokerGameMetadataRegistry.TryGet(PokerGameMetadataRegistry.HoldEmCode, out var metadata);

        // Assert
        metadata.Should().NotBeNull();
        metadata!.InitialHoleCards.Should().Be(2);
        metadata.MaxCommunityCards.Should().Be(5);
    }

    #endregion

    #region PokerGameRulesRegistry Tests

    [Theory]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode)]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode)]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode)]
    [InlineData(PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode)]
    public void RulesRegistry_TryGet_KnownGameType_ReturnsRules(string gameCode)
    {
        // Act
        var result = PokerGameRulesRegistry.TryGet(gameCode, out var rules);

        // Assert
        result.Should().BeTrue();
        rules.Should().NotBeNull();
        rules!.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public void RulesRegistry_TryGet_UnknownGameType_ReturnsFalse()
    {
        // Act
        var result = PokerGameRulesRegistry.TryGet("UNKNOWN", out var rules);

        // Assert
        result.Should().BeFalse();
        rules.Should().BeNull();
    }

    [Fact]
    public void RulesRegistry_FiveCardDraw_HasExpectedPhases()
    {
        // Act
        PokerGameRulesRegistry.TryGet(PokerGameMetadataRegistry.FiveCardDrawCode, out var rules);

        // Assert
        rules.Should().NotBeNull();
        rules!.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.CollectingAntes));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.Dealing));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.FirstBettingRound));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.DrawPhase));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.SecondBettingRound));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.Showdown));
    }

    [Fact]
    public void RulesRegistry_SevenCardStud_HasStreetPhases()
    {
        // Act
        PokerGameRulesRegistry.TryGet(PokerGameMetadataRegistry.SevenCardStudCode, out var rules);

        // Assert
        rules.Should().NotBeNull();
        rules!.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.ThirdStreet));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.FourthStreet));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.FifthStreet));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.SixthStreet));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.SeventhStreet));
    }

    [Fact]
    public void RulesRegistry_KingsAndLows_HasSpecialPhases()
    {
        // Act
        PokerGameRulesRegistry.TryGet(PokerGameMetadataRegistry.KingsAndLowsCode, out var rules);

        // Assert
        rules.Should().NotBeNull();
        rules!.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.DropOrStay));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.PotMatching));
        rules.Phases.Should().Contain(p => p.PhaseId == nameof(Phases.PlayerVsDeck));
    }

    #endregion
}
