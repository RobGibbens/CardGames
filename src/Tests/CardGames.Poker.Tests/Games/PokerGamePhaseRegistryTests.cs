using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.HoldEm;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public sealed class PokerGamePhaseRegistryTests
{
	[Fact]
	public void TryResolve_WhenHoldEmAndValidPhase_ReturnsHoldEmEnum()
	{
		var result = PokerGamePhaseRegistry.TryResolve("HOLDEM", "Flop", out var phase);

		result.Should().BeTrue();
		phase.Should().NotBeNull();
		phase.Should().BeOfType<HoldEmPhase>();
		((HoldEmPhase)phase!).Should().Be(HoldEmPhase.Flop);
	}

	[Fact]
	public void TryResolve_WhenGameTypeCodeUnknown_ReturnsFalse()
	{
		var result = PokerGamePhaseRegistry.TryResolve("UNKNOWN", "Flop", out var phase);

		result.Should().BeFalse();
		phase.Should().BeNull();
	}

	[Fact]
	public void TryResolve_WhenPhaseInvalidForGame_ReturnsFalse()
	{
		var result = PokerGamePhaseRegistry.
			TryResolve("HOLDEM", "NotARealPhase", out var phase);

		result.Should().BeFalse();
		phase.Should().BeNull();
	}

	[Fact]
	public void TryResolveGeneric_WhenMismatchedEnumType_ReturnsFalse()
	{
		var result = PokerGamePhaseRegistry.TryResolve(
			"HOLDEM",
			"Flop",
			out FiveCardDrawPhase _);

		result.Should().BeFalse();
	}
}
