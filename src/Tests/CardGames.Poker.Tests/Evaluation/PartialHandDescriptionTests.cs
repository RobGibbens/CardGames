using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class PartialHandDescriptionTests
{
	[Fact]
	public void TwoCards_HighCard_ReturnsCorrectDescription()
	{
		// Assemble
		var cards = new List<Card>
		{
			new(Suit.Hearts, Symbol.Deuce),
			new(Suit.Diamonds, Symbol.Ace)
		};
		var hand = new DrawHand(cards);

		// Act
		var result = HandDescriptionFormatter.GetHandDescription(hand);

		// Assert
		result.Should().Be("Ace high");
	}

	[Fact]
	public void ThreeCards_Pair_ReturnsCorrectDescription()
	{
		// Assemble
		var cards = new List<Card>
		{
			new(Suit.Hearts, Symbol.Four),
			new(Suit.Diamonds, Symbol.Four),
			new(Suit.Spades, Symbol.Ace)
		};
		var hand = new DrawHand(cards);

		// Act
		var result = HandDescriptionFormatter.GetHandDescription(hand);

		// Assert
		result.Should().Be("Pair of Fours");
	}

	[Fact]
	public void HoldemHand_TwoCards_ReturnsCorrectDescription()
	{
		// Assemble
		var holeCards = new List<Card>
		{
			new(Suit.Hearts, Symbol.King),
			new(Suit.Diamonds, Symbol.Queen)
		};
		var communityCards = new List<Card>(); // Empty
		var hand = new HoldemHand(holeCards, communityCards);

		// Act
		var result = HandDescriptionFormatter.GetHandDescription(hand);

		// Assert
		result.Should().Be("King high");
	}

	[Fact]
	public void StudHand_ThreeCards_ReturnsCorrectDescription()
	{
		// Assemble
		var holeCards = new List<Card>
		{
			new(Suit.Hearts, Symbol.Ten),
			new(Suit.Diamonds, Symbol.Ten)
		};
		var openCards = new List<Card>
		{
			new(Suit.Spades, Symbol.Deuce)
		};
		var downCards = new List<Card>();

		var hand = new StudHand(holeCards, openCards, downCards);

		// Act
		var result = HandDescriptionFormatter.GetHandDescription(hand);

		// Assert
		result.Should().Be("Pair of Tens");
	}
}