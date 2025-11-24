using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class FollowTheQueenWildCardRulesTests
{
    private readonly FollowTheQueenWildCardRules _rules = new();

    [Fact]
    public void Queens_Are_Always_Wild()
    {
        var hand = "Qh 5d 7c 8s Ts Jc Kd".ToCards();
        var faceUpCards = "5d 7c 8s Ts".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().Contain((int)Symbol.Queen);
    }

    [Fact]
    public void No_Queen_Face_Up_Only_Queens_Are_Wild()
    {
        var faceUpCards = "5d 7c 8s Ts".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(1);
        wildRanks.Should().Contain((int)Symbol.Queen);
    }

    [Fact]
    public void Queen_Face_Up_Makes_Following_Card_Rank_Wild()
    {
        // Queen dealt, then 5 is dealt next - 5s become wild
        var faceUpCards = "Qh 5d 7c 8s".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Five);
    }

    [Fact]
    public void Second_Queen_Replaces_Previous_Following_Wild()
    {
        // First Queen dealt, then 5 is dealt (5s wild), then second Queen, then 8 is dealt (8s now wild, not 5s)
        var faceUpCards = "Qh 5d Qc 8s".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Eight);
        wildRanks.Should().NotContain((int)Symbol.Five);
    }

    [Fact]
    public void Queen_As_Last_Face_Up_Card_Only_Queens_Wild()
    {
        // Queen is the last face-up card dealt
        var faceUpCards = "5d 7c 8s Qh".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(1);
        wildRanks.Should().Contain((int)Symbol.Queen);
    }

    [Fact]
    public void Multiple_Queens_With_Last_Queen_At_End_Only_Queens_Wild()
    {
        // Multiple Queens dealt, but last one is at the end with no following card
        var faceUpCards = "Qh 5d Qc 8s Qd".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(1);
        wildRanks.Should().Contain((int)Symbol.Queen);
    }

    [Fact]
    public void Card_Following_Last_Queen_Becomes_Wild()
    {
        // Multiple Queens, the card following the last Queen becomes wild
        var faceUpCards = "Qh 5d Qc Kd".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.King);
    }

    [Fact]
    public void Queen_Following_Queen_Only_Queens_Wild()
    {
        // Queen followed immediately by another Queen - only Queens are wild
        var faceUpCards = "Qh Qd 5c 8s".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Five);
    }

    [Fact]
    public void DetermineWildCards_Returns_Wild_Cards_From_Hand()
    {
        var hand = "Qh 5d 5c 8s Ts Jc Kd".ToCards();
        var faceUpCards = "Qh 5d 8s Ts".ToCards(); // Queen then 5 - 5s are wild

        var wildCards = _rules.DetermineWildCards(hand, faceUpCards);

        wildCards.Should().HaveCount(3); // Qh, 5d, 5c
        wildCards.Should().Contain(c => c.Symbol == Symbol.Queen);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void DetermineWildCards_With_No_Wild_Cards_In_Hand()
    {
        var hand = "3d 4c 6s 7h 8s Ts Jc".ToCards();
        var faceUpCards = "Kh 5d 8s Ts".ToCards(); // No Queens

        var wildCards = _rules.DetermineWildCards(hand, faceUpCards);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void Three_Queens_Face_Up_With_Following_Card()
    {
        var faceUpCards = "Qh 5d Qc 8s Qd 2c".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Deuce);
    }

    [Fact]
    public void Empty_Face_Up_Cards_Only_Queens_Wild()
    {
        var faceUpCards = System.Array.Empty<Card>();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(1);
        wildRanks.Should().Contain((int)Symbol.Queen);
    }

    [Fact]
    public void Wild_Rank_Is_Ace_When_Queen_Followed_By_Ace()
    {
        var faceUpCards = "Qh Ad 5c 8s".ToCards();

        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        wildRanks.Should().HaveCount(2);
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Ace);
    }

    [Fact]
    public void Wild_Cards_Include_All_Cards_Of_Wild_Rank()
    {
        // Queen followed by 7, player has three 7s
        var hand = "Qh 7d 7c 7s 8s Ts Jc".ToCards();
        var faceUpCards = "Qh 7d 8s Ts".ToCards();

        var wildCards = _rules.DetermineWildCards(hand, faceUpCards);

        wildCards.Should().HaveCount(4); // Qh, 7d, 7c, 7s
        wildCards.Count(c => c.Symbol == Symbol.Seven).Should().Be(3);
        wildCards.Count(c => c.Symbol == Symbol.Queen).Should().Be(1);
    }
}
