using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class PairPressureWildCardRulesTests
{
    [Fact]
    public void First_Paired_Rank_Becomes_Wild()
    {
        var faceUpCards = "8h 8d As Kc".ToCards();

        var wildRanks = DetermineWildRanks(faceUpCards);

        wildRanks.Should().ContainSingle();
        wildRanks.Should().Contain((int)Symbol.Eight);
    }

    [Fact]
    public void Only_Two_Most_Recent_Distinct_Paired_Ranks_Are_Wild()
    {
        var faceUpCards = "8h 8d 5c 5s Kh Kd".ToCards();

        var wildRanks = DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Five);
        wildRanks.Should().Contain((int)Symbol.King);
        wildRanks.Should().NotContain((int)Symbol.Eight);
    }

    [Fact]
    public void Repeating_An_Existing_Paired_Rank_Does_Not_Reintroduce_An_Evicted_Rank()
    {
        var faceUpCards = "8h 8d 5c 5s Kh Kd Ks".ToCards();

        var wildRanks = DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Five);
        wildRanks.Should().Contain((int)Symbol.King);
        wildRanks.Should().NotContain((int)Symbol.Eight);
    }

    [Fact]
    public void DetermineWildCards_Returns_All_Hand_Cards_Matching_Tracked_Wild_Ranks()
    {
        var hand = "8c 5h Ks Ad 2d 5d Kh".ToCards();
        var faceUpCards = "8h 8d 5c 5s Kh Kd".ToCards();

        var wildCards = DetermineWildCards(hand, faceUpCards);

        wildCards.Should().HaveCount(4);
        wildCards.Count(card => card.Symbol == Symbol.Five).Should().Be(2);
        wildCards.Count(card => card.Symbol == Symbol.King).Should().Be(2);
        wildCards.Should().NotContain(card => card.Symbol == Symbol.Eight);
    }

    private static IReadOnlyCollection<int> DetermineWildRanks(IReadOnlyCollection<Card> faceUpCards)
    {
        var rules = CreateRules();
        var method = rules.GetType().GetMethod("DetermineWildRanks");

        method.Should().NotBeNull("Pair Pressure should expose DetermineWildRanks once the backend variant lands.");

        var result = method!.Invoke(rules, [faceUpCards]);

        result.Should().BeAssignableTo<IReadOnlyCollection<int>>();
        return (IReadOnlyCollection<int>)result!;
    }

    private static IReadOnlyCollection<Card> DetermineWildCards(IReadOnlyCollection<Card> hand, IReadOnlyCollection<Card> faceUpCards)
    {
        var rules = CreateRules();
        var method = rules.GetType().GetMethod("DetermineWildCards");

        method.Should().NotBeNull("Pair Pressure should expose DetermineWildCards once the backend variant lands.");

        var result = method!.Invoke(rules, [hand, faceUpCards]);

        result.Should().BeAssignableTo<IReadOnlyCollection<Card>>();
        return (IReadOnlyCollection<Card>)result!;
    }

    private static object CreateRules()
    {
        var assembly = typeof(FollowTheQueenWildCardRules).Assembly;
        var type = assembly.GetType("CardGames.Poker.Hands.WildCards.PairPressureWildCardRules");

        type.Should().NotBeNull("Pair Pressure wild-card rules should be implemented in the poker assembly.");

        var instance = Activator.CreateInstance(type!);
        instance.Should().NotBeNull();
        return instance!;
    }
}