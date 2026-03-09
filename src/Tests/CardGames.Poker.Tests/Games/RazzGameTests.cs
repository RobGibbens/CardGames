using CardGames.Poker.Games;
using CardGames.Poker.Games.Razz;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class RazzGameTests
{
    [Fact]
    public void Metadata_MatchesExpectedValues()
    {
        var game = new RazzGame();

        game.Name.Should().Be("Razz");
        game.VariantType.Should().Be(VariantType.Stud);
        game.MinimumNumberOfPlayers.Should().Be(2);
        game.MaximumNumberOfPlayers.Should().Be(7);
    }

    [Fact]
    public void GetGameRules_ReturnsRazzRules()
    {
        var game = new RazzGame();

        var rules = game.GetGameRules();

        rules.GameTypeCode.Should().Be("RAZZ");
        rules.GameTypeName.Should().Be("Razz");
        rules.Betting.HasAntes.Should().BeTrue();
    }
}
