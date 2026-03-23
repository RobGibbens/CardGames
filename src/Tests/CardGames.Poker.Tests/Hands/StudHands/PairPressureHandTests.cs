using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class PairPressureHandTests
{
    [Fact]
    public void Hand_With_Two_Tracked_Paired_Ranks_Uses_Both_As_Wild()
    {
        var holeCards = "5h Kd".ToCards();
        var openCards = "As Ah 2c 3d".ToCards();
        var downCard = "Ad".ToCard();
        var faceUpCards = "8h 8d 5c 5s Kh Kc".ToCards();

        var hand = CreateHand(holeCards, openCards, downCard, faceUpCards);

        GetWildCards(hand).Should().HaveCount(2);
        GetWildCards(hand).Should().Contain(card => card.Symbol == Symbol.Five);
        GetWildCards(hand).Should().Contain(card => card.Symbol == Symbol.King);
        GetHandType(hand).Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_Without_FaceUp_Pairs_Evaluates_Naturally()
    {
        var holeCards = "As Ah".ToCards();
        var openCards = "3c 5s 7h 9d".ToCards();
        var downCard = "Jc".ToCard();
        var faceUpCards = "3c 5s 7h 9d".ToCards();

        var hand = CreateHand(holeCards, openCards, downCard, faceUpCards);

        GetWildCards(hand).Should().BeEmpty();
        GetHandType(hand).Should().Be(HandType.OnePair);
    }

    [Fact]
    public void Older_Evicted_Paired_Rank_Is_Not_Treated_As_Wild_During_Hand_Evaluation()
    {
        var holeCards = "8h Kd".ToCards();
        var openCards = "As Ah Ad 2c".ToCards();
        var downCard = "3d".ToCard();
        var faceUpCards = "8c 8s 5c 5s Kh Kc".ToCards();

        var hand = CreateHand(holeCards, openCards, downCard, faceUpCards);
        var wildCards = GetWildCards(hand);

        wildCards.Should().ContainSingle(card => card.Symbol == Symbol.King);
        wildCards.Should().NotContain(card => card.Symbol == Symbol.Eight);
        GetHandType(hand).Should().Be(HandType.Quads);
    }

    private static object CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard,
        IReadOnlyCollection<Card> faceUpCards)
    {
        var assembly = typeof(SevenCardStudHand).Assembly;
        var type = assembly.GetType("CardGames.Poker.Hands.StudHands.PairPressureHand");

        type.Should().NotBeNull("Pair Pressure hand evaluation should be implemented in the poker assembly.");

        var instance = Activator.CreateInstance(type!, holeCards.ToList(), openCards.ToList(), downCard, faceUpCards.ToList());

        instance.Should().NotBeNull("Pair Pressure should follow the Follow the Queen-style stud hand constructor shape.");
        return instance!;
    }

    private static IReadOnlyCollection<Card> GetWildCards(object hand)
    {
        var property = hand.GetType().GetProperty("WildCards");

        property.Should().NotBeNull("Pair Pressure hand should expose WildCards for UI/showdown highlighting.");

        var value = property!.GetValue(hand);
        value.Should().BeAssignableTo<IReadOnlyCollection<Card>>();
        return (IReadOnlyCollection<Card>)value!;
    }

    private static HandType GetHandType(object hand)
    {
        var property = hand.GetType().GetProperty("Type");

        property.Should().NotBeNull();

        var value = property!.GetValue(hand);
        value.Should().BeOfType<HandType>();
        return (HandType)value!;
    }
}