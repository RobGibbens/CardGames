using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
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
		phase.Should().BeOfType<Phases>();
		((Phases)phase!).Should().Be(Phases.Flop);
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
			out System.DayOfWeek _);

		result.Should().BeFalse();
	}
}
