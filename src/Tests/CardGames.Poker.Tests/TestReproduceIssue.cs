using CardGames.Core.French.Cards.Extensions;
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
    public void Tony_Hand_From_Issue_Should_Evaluate()
    {
        // Tony's hand: (hole: Jd 7d 3s) (board: Qh 7h 5d 3d) - Wild cards: 3s 3d
        var holeCards = "7d 3s".ToCards();  // Initial hole cards
        var openCards = "Qh 7h 5d 3d".ToCards();
        var downCards = "Jd".ToCards();  // Final down card

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        
        // Should not throw
        var type = hand.Type;
        var strength = hand.Strength;
        
        _output.WriteLine($"Type: {type}");
        _output.WriteLine($"Strength: {strength}");
    }

    [Fact]
    public void Eric_Hand_From_Issue_Should_Evaluate()
    {
        // Eric's hand: (hole: Kh 8c 3h) (board: Td 8d 6d 4d 2s) - Wild cards: 3h
        var holeCards = "8c 3h".ToCards();
        var openCards = "Td 8d 6d 4d 2s".ToCards();
        var downCards = "Kh".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        
        var type = hand.Type;
        var strength = hand.Strength;
        
        _output.WriteLine($"Type: {type}");
        _output.WriteLine($"Strength: {strength}");
    }

    [Fact]
    public void Goose_Hand_From_Issue_Should_Evaluate()
    {
        // Goose's hand: (hole: Qs 4c 2c) (board: 9d 8s 7s 2d) - Wild cards: 9d
        var holeCards = "4c 2c".ToCards();
        var openCards = "9d 8s 7s 2d".ToCards();
        var downCards = "Qs".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        
        var type = hand.Type;
        var strength = hand.Strength;
        
        _output.WriteLine($"Type: {type}");
        _output.WriteLine($"Strength: {strength}");
    }

    [Fact]
    public void Rob_Hand_From_Issue_Should_Evaluate()
    {
        // Rob's hand: (hole: Tc 6s 5s) (board: Ad Ac 9h 6h) - Wild cards: 9h
        var holeCards = "Tc 5s".ToCards();
        var openCards = "Ad Ac 9h 6h".ToCards();
        var downCards = "6s".ToCards();

        var hand = new BaseballHand(holeCards, openCards, downCards);
        
        _output.WriteLine($"Cards: {string.Join(" ", hand.Cards)}");
        _output.WriteLine($"Wild cards: {string.Join(" ", hand.WildCards)}");
        
        var type = hand.Type;
        var strength = hand.Strength;
        
        _output.WriteLine($"Type: {type}");
        _output.WriteLine($"Strength: {strength}");
    }
}
