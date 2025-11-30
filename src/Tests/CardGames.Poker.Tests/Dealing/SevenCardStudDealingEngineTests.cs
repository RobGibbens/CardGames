using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Dealing;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Dealing;

public class SevenCardStudDealingEngineTests
{
    private static readonly List<string> TwoPlayers = ["Alice", "Bob"];
    private static readonly List<string> FourPlayers = ["Alice", "Bob", "Charlie", "Diana"];

    [Fact]
    public void Initialize_WithValidParameters_SetsProperties()
    {
        var engine = new SevenCardStudDealingEngine();
        
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        
        engine.VariantId.Should().Be("seven-card-stud");
        engine.HoleCardsPerPlayer.Should().Be(3);
        engine.UsesCommunityCards.Should().BeFalse();
        engine.UsesBurnCards.Should().BeFalse();
    }

    [Fact]
    public void Initialize_WithSeed_StoresCorrectSeed()
    {
        var engine = new SevenCardStudDealingEngine();
        
        engine.Initialize(TwoPlayers, dealerPosition: 0, seed: 12345);
        
        engine.Seed.Should().Be(12345);
    }

    [Fact]
    public void Initialize_ThrowsForTooFewPlayers()
    {
        var engine = new SevenCardStudDealingEngine();
        
        var act = () => engine.Initialize(["Solo"], dealerPosition: 0);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Initialize_ThrowsForTooManyPlayers()
    {
        var engine = new SevenCardStudDealingEngine();
        var ninePlayers = Enumerable.Range(1, 9).Select(i => $"Player{i}").ToList();
        
        var act = () => engine.Initialize(ninePlayers, dealerPosition: 0);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at most 8 players*");
    }

    [Fact]
    public void Shuffle_ResetsCardsRemaining()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        
        engine.Shuffle();
        
        engine.CardsRemaining.Should().Be(52);
    }

    [Fact]
    public void DealHoleCards_TwoPlayers_DealsCorrectNumberOfCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // 2 players × 3 cards (2 hole + 1 face up) = 6 cards
        results.Should().HaveCount(6);
    }

    [Fact]
    public void DealHoleCards_DealsCorrectCardTypes()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // First 4 cards should be hole cards (2 per player)
        results.Take(4).All(r => r.CardType == DealCardType.HoleCard).Should().BeTrue();
        // Last 2 cards should be face-up cards (door cards)
        results.Skip(4).All(r => r.CardType == DealCardType.FaceUpCard).Should().BeTrue();
    }

    [Fact]
    public void DealHoleCards_FourPlayers_DealsInCorrectOrder()
    {
        var engine = new SevenCardStudDealingEngine();
        // Dealer at position 0 (Alice), so dealing starts with position 1 (Bob)
        engine.Initialize(FourPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // 4 players × 3 cards = 12 cards
        results.Should().HaveCount(12);
        
        // First round hole cards: Bob, Charlie, Diana, Alice
        results[0].Recipient.Should().Be("Bob");
        results[1].Recipient.Should().Be("Charlie");
        results[2].Recipient.Should().Be("Diana");
        results[3].Recipient.Should().Be("Alice");
        
        // Second round hole cards: Bob, Charlie, Diana, Alice
        results[4].Recipient.Should().Be("Bob");
        results[5].Recipient.Should().Be("Charlie");
        
        // Door cards: Bob, Charlie, Diana, Alice
        results[8].Recipient.Should().Be("Bob");
        results[9].Recipient.Should().Be("Charlie");
        results[10].Recipient.Should().Be("Diana");
        results[11].Recipient.Should().Be("Alice");
    }

    [Fact]
    public void DealStreet_FourthStreet_DealsFaceUpCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        
        var results = engine.DealStreet(4, TwoPlayers);
        
        results.Should().HaveCount(2);
        results.All(r => r.CardType == DealCardType.FaceUpCard).Should().BeTrue();
    }

    [Fact]
    public void DealStreet_FifthStreet_DealsFaceUpCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        engine.DealStreet(4, TwoPlayers);
        
        var results = engine.DealStreet(5, TwoPlayers);
        
        results.Should().HaveCount(2);
        results.All(r => r.CardType == DealCardType.FaceUpCard).Should().BeTrue();
    }

    [Fact]
    public void DealStreet_SixthStreet_DealsFaceUpCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        engine.DealStreet(4, TwoPlayers);
        engine.DealStreet(5, TwoPlayers);
        
        var results = engine.DealStreet(6, TwoPlayers);
        
        results.Should().HaveCount(2);
        results.All(r => r.CardType == DealCardType.FaceUpCard).Should().BeTrue();
    }

    [Fact]
    public void DealStreet_SeventhStreet_DealsFaceDownCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        engine.DealStreet(4, TwoPlayers);
        engine.DealStreet(5, TwoPlayers);
        engine.DealStreet(6, TwoPlayers);
        
        var results = engine.DealStreet(7, TwoPlayers);
        
        results.Should().HaveCount(2);
        results.All(r => r.CardType == DealCardType.HoleCard).Should().BeTrue();
    }

    [Fact]
    public void DealStreet_InvalidStreetNumber_Throws()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        
        var act = () => engine.DealStreet(3, TwoPlayers);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*between 4 and 7*");
    }

    [Fact]
    public void DealStreet_OutOfOrder_Throws()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        
        var act = () => engine.DealStreet(5, TwoPlayers); // Skipping 4th street
        
        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*Expected to deal street 4*");
    }

    [Fact]
    public void DealCommunityCards_ThrowsNotSupported()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var act = () => engine.DealCommunityCards("Flop");
        
        act.Should().Throw<System.InvalidOperationException>()
            .WithMessage("*does not use community cards*");
    }

    [Fact]
    public void DealToPlayer_FaceDown_DealsHoleCard()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var result = engine.DealToPlayer("Alice", faceUp: false);
        
        result.Recipient.Should().Be("Alice");
        result.CardType.Should().Be(DealCardType.HoleCard);
        result.Card.Should().NotBeNull();
    }

    [Fact]
    public void DealToPlayer_FaceUp_DealsFaceUpCard()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var result = engine.DealToPlayer("Alice", faceUp: true);
        
        result.CardType.Should().Be(DealCardType.FaceUpCard);
    }

    [Fact]
    public void FullDealSequence_DealsCorrectTotalCards()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        // Third street: 6 cards (2 players × 3 cards = 2 hole + 1 door each)
        engine.DealHoleCards();
        // Fourth street: 1 face-up card per player = 2 cards
        engine.DealStreet(4, TwoPlayers);
        // Fifth street: 1 face-up card per player = 2 cards
        engine.DealStreet(5, TwoPlayers);
        // Sixth street: 1 face-up card per player = 2 cards
        engine.DealStreet(6, TwoPlayers);
        // Seventh street: 1 face-down card per player = 2 cards
        engine.DealStreet(7, TwoPlayers);
        
        // Total cards dealt per player: 7 (standard seven card stud)
        // Total dealt: 2 players × 7 cards = 14 cards
        engine.CardsRemaining.Should().Be(52 - 14);
    }

    [Fact]
    public void ReplayBySeed_Reproducible()
    {
        // First deal with seed
        var engine1 = new SevenCardStudDealingEngine();
        engine1.Initialize(TwoPlayers, dealerPosition: 0, seed: 42);
        engine1.Shuffle();
        var results1 = engine1.DealHoleCards();
        
        // Second deal with same seed
        var engine2 = new SevenCardStudDealingEngine();
        engine2.Initialize(TwoPlayers, dealerPosition: 0, seed: 42);
        engine2.Shuffle();
        var results2 = engine2.DealHoleCards();
        
        // Cards should be identical
        for (int i = 0; i < results1.Count; i++)
        {
            results1[i].Card.Should().Be(results2[i].Card);
        }
    }

    [Fact]
    public void GetStreetName_ReturnsCorrectNames()
    {
        SevenCardStudDealingEngine.GetStreetName(3).Should().Be("Third Street");
        SevenCardStudDealingEngine.GetStreetName(4).Should().Be("Fourth Street");
        SevenCardStudDealingEngine.GetStreetName(5).Should().Be("Fifth Street");
        SevenCardStudDealingEngine.GetStreetName(6).Should().Be("Sixth Street");
        SevenCardStudDealingEngine.GetStreetName(7).Should().Be("Seventh Street");
    }

    [Fact]
    public void DealHoleCards_AllCardsUnique()
    {
        var engine = new SevenCardStudDealingEngine();
        engine.Initialize(FourPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        var cards = results.Select(r => r.Card).ToList();
        cards.Should().OnlyHaveUniqueItems();
    }
}
