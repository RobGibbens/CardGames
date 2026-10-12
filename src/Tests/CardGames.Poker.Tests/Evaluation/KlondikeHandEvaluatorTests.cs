using System.Linq;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class KlondikeHandEvaluatorTests
{
	private readonly KlondikeHandEvaluator _evaluator = new();

	[Fact]
	public void CreateHand_WithHoleAndBoardCards_ReturnsKlondikeHand()
	{
		var holeCards = "Ah Kh".ToCards();
		var boardCards = "Th Qh Jh 9c 2d".ToCards(); // first card is Klondike Card

		var hand = _evaluator.CreateHand(holeCards, boardCards, []);

		hand.Should().BeOfType<KlondikeHand>();
	}

	[Fact]
	public void CreateHand_WithFlatCardList_ReturnsKlondikeHand()
	{
		// 2 hole + 4 community (first community card is Klondike Card)
		var cards = "Ah Kh 2d Qh Jh 9c".ToCards();

		var hand = _evaluator.CreateHand(cards);

		hand.Should().BeOfType<KlondikeHand>();
	}

	[Fact]
	public void HasWildCards_ReturnsTrue()
	{
		_evaluator.HasWildCards.Should().BeTrue();
	}

	[Fact]
	public void SupportsPositionalCards_ReturnsTrue()
	{
		_evaluator.SupportsPositionalCards.Should().BeTrue();
	}

	[Fact]
	public void GetWildCardIndexes_ReturnsFirstCommunityCardIndex()
	{
		var cards = "Ah Kh Th Qh Jh 9c 2d".ToCards(); // 7 cards total: 2 hole + 5 community

		var indexes = _evaluator.GetWildCardIndexes(cards);

		indexes.Should().ContainSingle().Which.Should().Be(2); // first community card index (after 2 hole cards)
	}

	[Fact]
	public void GetWildCardIndexes_HoleDeuceMatchingKlondikeRank_ReturnsOnlyKlondikeIndex()
	{
		var cards = "2h 4c 2c Ks 7d 8c 3h 9c".ToCards();

		var indexes = _evaluator.GetWildCardIndexes(cards);

		indexes.Should().ContainSingle().Which.Should().Be(2);
	}

	[Fact]
	public void KlondikeHand_SecondDeuceIsNotWild_TwoPairBeatsPair()
	{
		var communityCards = "2c Ks 7d 8c 3h 9c".ToCards();
		var klondikeCard = communityCards.First();
		var ann = new KlondikeHand("2h 4c".ToCards(), communityCards, klondikeCard);
		var ben = new KlondikeHand("Ah 9d".ToCards(), communityCards, klondikeCard);

		ann.Type.Should().Be(HandType.OnePair);
		HandDescriptionFormatter.GetHandDescription(ann).Should().Be("Pair of Kings");
		ben.Type.Should().Be(HandType.Trips);
		HandDescriptionFormatter.GetHandDescription(ben).Should().Be("Three of a kind, Nines");
		ben.Strength.Should().BeGreaterThan(ann.Strength);
	}

	[Fact]
	public void GetWildCardIndexes_WithFewerThanThreeCards_ReturnsEmpty()
	{
		var indexes = _evaluator.GetWildCardIndexes([]);

		indexes.Should().BeEmpty();
	}

	[Fact]
	public void KlondikeHand_WithWildCard_ImprovesStraight()
	{
		// Hole: A♥ K♥, Community: Q♥ J♥ 5♣ (Klondike=5♣), T♥, 8♠
		// Without wild: A K Q J T = straight (Ace-high)
		// With wild card (5♣ becomes anything): should still make at least a straight
		var holeCards = "Ah Kh".ToCards();
		var communityCards = "Qh Jh 5c Th 8s".ToCards();
		var communityList = communityCards.ToList();
		var klondikeCard = communityList[2]; // 5♣ is the Klondike Card

		var hand = new KlondikeHand(holeCards, communityList, klondikeCard);

		// A♥ K♥ Q♥ J♥ T♥ already forms a natural straight flush, wild card makes it even better
		hand.Type.Should().Be(HandType.StraightFlush);
	}

	[Fact]
	public void KlondikeHand_WithWildCard_MakesFourOfAKind()
	{
		// Hole: K♥ K♠, Community: K♦ 2♣ (wild) 5♠ 8♦
		// Without wild: three kings
		// With wild (2♣ acts as K♣): four of a kind kings
		var holeCards = "Kh Ks".ToCards();
		var communityCards = "Kd 2c 5s 8d".ToCards();
		var communityList = communityCards.ToList();
		var klondikeCard = communityList[1]; // 2♣ is the Klondike Card

		var hand = new KlondikeHand(holeCards, communityList, klondikeCard);

		hand.Type.Should().Be(HandType.Quads);
	}

	[Fact]
	public void GetEvaluatedBestCards_ReturnsEvaluatedCards()
	{
		var holeCards = "Ah Kh".ToCards();
		var boardCards = "Th Qh Jh 9c 2d".ToCards(); // Klondike card (Th) is first

		var hand = (KlondikeHand)_evaluator.CreateHand(holeCards, boardCards, []);
		var bestCards = _evaluator.GetEvaluatedBestCards(hand);

		bestCards.Should().HaveCount(5, "best hand should be exactly 5 cards");
	}
}