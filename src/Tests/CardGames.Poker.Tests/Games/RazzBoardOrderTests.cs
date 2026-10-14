using CardGames.Core.French.Cards;
using CardGames.Poker.Games.Razz;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class RazzBoardOrderTests
{
	[Fact]
	public void BringIn_KingBringsInOverAce()
	{
		var king = new Card(Suit.Clubs, Symbol.King);
		var ace = new Card(Suit.Spades, Symbol.Ace);

		RazzBoardOrder.BringsInOver(king, ace).Should().BeTrue();
		RazzBoardOrder.BringsInOver(ace, king).Should().BeFalse();
	}

	[Fact]
	public void BringIn_SpadeKingBringsInOverHeartKing()
	{
		var spade = new Card(Suit.Spades, Symbol.King);
		var heart = new Card(Suit.Hearts, Symbol.King);

		RazzBoardOrder.BringsInOver(spade, heart).Should().BeTrue();
		RazzBoardOrder.BringsInOver(heart, spade).Should().BeFalse();
	}

	[Fact]
	public void LaterStreet_EightHighActsBeforeAPairOfDeuces()
	{
		var eightHigh = RazzBoardOrder.LowBoardStrength(
			[new Card(Suit.Clubs, Symbol.Eight), new Card(Suit.Diamonds, Symbol.Seven)]);
		var deuces = RazzBoardOrder.LowBoardStrength(
			[new Card(Suit.Hearts, Symbol.Deuce), new Card(Suit.Spades, Symbol.Deuce)]);

		eightHigh.Should().BeGreaterThan(deuces);
	}

	[Fact]
	public void LaterStreet_AceDoorActsBeforeAKing()
	{
		var ace = RazzBoardOrder.LowBoardStrength([new Card(Suit.Hearts, Symbol.Ace)]);
		var king = RazzBoardOrder.LowBoardStrength([new Card(Suit.Spades, Symbol.King)]);

		ace.Should().BeGreaterThan(king);
	}
}
