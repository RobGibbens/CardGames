using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Games;

public class StudGameStakesNormalizerTests
{
	[Theory]
	[InlineData("SEVENCARDSTUD", 5, 10, 5, 10, 20)]
	[InlineData("SEVENCARDSTUD", 10, 20, 10, 20, 40)]
	[InlineData("RAZZ", 5, 10, 5, 10, 20)]
	[InlineData("FOLLOWTHEQUEEN", 5, 10, 5, 10, 20)]
	[InlineData("PAIRPRESSURE", 5, 10, 5, 10, 20)]
	[InlineData("BASEBALL", 5, 10, 5, 10, 20)]
	[InlineData("TOLLBOOTH", 5, 10, 5, 10, 20)]
	public void ApplyToGame_HasBringIn_DerivesStakesFromAnteAndMinBet(
		string gameCode,
		int ante,
		int minBet,
		int expectedBringIn,
		int expectedSmallBet,
		int expectedBigBet)
	{
		var game = NewGame();
		var rules = PokerGameRulesRegistry.Get(gameCode);

		StudGameStakesNormalizer.ApplyToGame(game, rules, ante, minBet);

		game.BringIn.Should().Be(expectedBringIn);
		game.SmallBet.Should().Be(expectedSmallBet);
		game.BigBet.Should().Be(expectedBigBet);
	}

	[Fact]
	public void ApplyToGame_ZeroAnte_UsesHalfSmallBet()
	{
		var game = NewGame();
		var rules = PokerGameRulesRegistry.Get("SEVENCARDSTUD");

		StudGameStakesNormalizer.ApplyToGame(game, rules, ante: 0, minBet: 10);

		game.BringIn.Should().Be(5);
		game.SmallBet.Should().Be(10);
		game.BigBet.Should().Be(20);
	}

	[Fact]
	public void ApplyToGame_GoodBadUgly_SetsStreetBetsWithoutBringIn()
	{
		var game = NewGame();
		var rules = PokerGameRulesRegistry.Get("GOODBADUGLY");

		StudGameStakesNormalizer.ApplyToGame(game, rules, ante: 5, minBet: 10);

		game.BringIn.Should().BeNull();
		game.SmallBet.Should().Be(10);
		game.BigBet.Should().Be(20);
		StudGameStakesNormalizer.TryGetHasBringIn(rules).Should().BeFalse();
	}

	[Theory]
	[InlineData("HOLDEM")]
	[InlineData("FIVECARDDRAW")]
	public void ApplyToGame_NonStud_LeavesStakesUnset(string gameCode)
	{
		var game = NewGame();
		var rules = PokerGameRulesRegistry.Get(gameCode);

		StudGameStakesNormalizer.ApplyToGame(game, rules, ante: 5, minBet: 10);

		game.BringIn.Should().BeNull();
		game.SmallBet.Should().BeNull();
		game.BigBet.Should().BeNull();
	}

	private static Game NewGame()
	{
		return new Game
		{
			CurrentPhase = nameof(Phases.WaitingToStart)
		};
	}
}
