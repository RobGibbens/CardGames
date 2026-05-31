using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Games;

/// <summary>
/// Tests for <see cref="GameContinuationPolicy"/>, the authoritative source of truth for whether a
/// game session can continue. This logic previously lived inside <c>GetGameMapper</c> as a "simplified"
/// projection-layer heuristic; these tests protect it now that it owns a clear boundary.
/// </summary>
public class GameContinuationPolicyTests
{
	[Theory]
	[InlineData(0, false)]
	[InlineData(1, false)]
	[InlineData(2, true)]
	[InlineData(5, true)]
	public void CanContinue_WithPlayerCount_AppliesMinimumThreshold(int playersWithChips, bool expected)
	{
		GameContinuationPolicy.CanContinue(playersWithChips).Should().Be(expected);
	}

	[Fact]
	public void CanContinue_WithTwoPlayersHoldingChips_ReturnsTrue()
	{
		var players = new[]
		{
			new GamePlayer { ChipStack = 100 },
			new GamePlayer { ChipStack = 50 }
		};

		GameContinuationPolicy.CanContinue(players).Should().BeTrue();
	}

	[Fact]
	public void CanContinue_WithOnlyOnePlayerHoldingChips_ReturnsFalse()
	{
		var players = new[]
		{
			new GamePlayer { ChipStack = 100 },
			new GamePlayer { ChipStack = 0 },
			new GamePlayer { ChipStack = 0 }
		};

		GameContinuationPolicy.CanContinue(players).Should().BeFalse();
	}

	[Fact]
	public void CanContinue_WithNullPlayers_ReturnsFalse()
	{
		GameContinuationPolicy.CanContinue(players: null!).Should().BeFalse();
	}

	[Fact]
	public void MinimumPlayersToContinue_IsTwo()
	{
		GameContinuationPolicy.MinimumPlayersToContinue.Should().Be(2);
	}
}
