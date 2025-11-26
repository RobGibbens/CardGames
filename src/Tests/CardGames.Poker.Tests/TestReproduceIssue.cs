using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class BaseballHandIssueTests
{
    private readonly ITestOutputHelper _output;

    public BaseballHandIssueTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Rob_Hand_Should_Be_Three_Kings_Not_Straight()
    {
        // From the issue:
        // Rob's hand: (hole: 9s 5h 2d) (board: Ks 8c 6h 3c) Wild cards: 9s 3c
        // Issue says this was evaluated as "Straight (King-high)" 
        // But should be "Three Kings" (Ks + 2 wilds = K K K + 2 kickers)
        // K-high straight would need K-Q-J-T-9, but we only have K and 2 wilds
        var holeCards = "5h 2d".ToCards();
        var openCards = "Ks 8c 6h 3c".ToCards();
        var downCards = "9s".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        _output.WriteLine($"Type: {hand.Type}");
        _output.WriteLine($"Strength: {hand.Strength}");
        
        // Natural cards: Ks, 8c, 6h, 5h, 2d
        // Wild cards: 9s, 3c (2 wilds)
        // Best hand: Three Kings (Ks + 2 wilds as Kings)
        // NOT a straight - K-?-?-?-? would need 4 consecutive values
        hand.Type.Should().Be(HandType.Trips);
    }

    [Fact]
    public void Goose_Hand_Should_Be_Four_Queens_Not_Four_Aces()
    {
        // From the issue:
        // Goose's hand: (hole: Qd 9d 7d) (board: As Qc Qh 4h 2h) Wild cards: 9d
        // Issue says this was evaluated as "Four of a Kind (Aces)"
        // But should be "Four Queens" (Qd, Qc, Qh + 1 wild = Q Q Q Q)
        // NOT four Aces because there's only 1 Ace and 1 wild
        var holeCards = "Qd 7d".ToCards();
        var openCards = "As Qc Qh 4h 2h".ToCards();
        var downCards = "9d".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        _output.WriteLine($"Type: {hand.Type}");
        _output.WriteLine($"Strength: {hand.Strength}");
        
        // Natural cards: Qd, 7d, As, Qc, Qh, 4h, 2h (3 Queens, 1 Ace)
        // Wild cards: 9d (1 wild)
        // Best hand: Four Queens (3 Queens + 1 wild)
        hand.Type.Should().Be(HandType.Quads);
    }

    [Fact]
    public void Eric_Hand_Should_Be_Pair_Of_Kings()
    {
        // Eric's hand: (hole: Jd 8h 5s) (board: Ad Kd Kh 7s)
        // No wilds, should be "Pair of Kings"
        var holeCards = "Jd 8h".ToCards();
        var openCards = "Ad Kd Kh 7s".ToCards();
        var downCards = "5s".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        _output.WriteLine($"Type: {hand.Type}");
        _output.WriteLine($"Strength: {hand.Strength}");
        
        // No wilds
        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.OnePair);
    }
}
