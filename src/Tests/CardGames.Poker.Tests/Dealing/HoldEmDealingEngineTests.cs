using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Dealing;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Dealing;

public class HoldEmDealingEngineTests
{
    private static readonly List<string> TwoPlayers = ["Alice", "Bob"];
    private static readonly List<string> ThreePlayers = ["Alice", "Bob", "Charlie"];

    [Fact]
    public void Initialize_WithValidParameters_SetsProperties()
    {
        var engine = new HoldEmDealingEngine();
        
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        
        engine.VariantId.Should().Be("holdem");
        engine.HoleCardsPerPlayer.Should().Be(2);
        engine.UsesCommunityCards.Should().BeTrue();
        engine.UsesBurnCards.Should().BeTrue();
    }

    [Fact]
    public void Initialize_WithSeed_StoresCorrectSeed()
    {
        var engine = new HoldEmDealingEngine();
        
        engine.Initialize(TwoPlayers, dealerPosition: 0, seed: 12345);
        
        engine.Seed.Should().Be(12345);
    }

    [Fact]
    public void Initialize_ThrowsForTooFewPlayers()
    {
        var engine = new HoldEmDealingEngine();
        
        var act = () => engine.Initialize(["Solo"], dealerPosition: 0);
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2 players*");
    }

    [Fact]
    public void Shuffle_ResetsCardsRemaining()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        
        engine.Shuffle();
        
        engine.CardsRemaining.Should().Be(52);
    }

    [Fact]
    public void DealHoleCards_TwoPlayers_DealsCorrectNumberOfCards()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // 2 players × 2 cards = 4 cards
        results.Should().HaveCount(4);
        results.All(r => r.CardType == DealCardType.HoleCard).Should().BeTrue();
    }

    [Fact]
    public void DealHoleCards_ThreePlayers_DealsCorrectNumberOfCards()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(ThreePlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // 3 players × 2 cards = 6 cards
        results.Should().HaveCount(6);
    }

    [Fact]
    public void DealHoleCards_DealsInCorrectOrder_LeftOfDealer()
    {
        var engine = new HoldEmDealingEngine();
        // Dealer at position 0 (Alice), so dealing starts with position 1 (Bob)
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // First round: Bob, Alice; Second round: Bob, Alice
        results[0].Recipient.Should().Be("Bob");
        results[1].Recipient.Should().Be("Alice");
        results[2].Recipient.Should().Be("Bob");
        results[3].Recipient.Should().Be("Alice");
    }

    [Fact]
    public void DealHoleCards_ThreePlayers_DealsInCorrectOrder()
    {
        var engine = new HoldEmDealingEngine();
        // Dealer at position 1 (Bob), so dealing starts with position 2 (Charlie)
        engine.Initialize(ThreePlayers, dealerPosition: 1);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        // First round: Charlie, Alice, Bob
        results[0].Recipient.Should().Be("Charlie");
        results[1].Recipient.Should().Be("Alice");
        results[2].Recipient.Should().Be("Bob");
        // Second round: Charlie, Alice, Bob
        results[3].Recipient.Should().Be("Charlie");
        results[4].Recipient.Should().Be("Alice");
        results[5].Recipient.Should().Be("Bob");
    }

    [Fact]
    public void DealHoleCards_AssignsSequentialDealSequence()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        var sequences = results.Select(r => r.DealSequence).ToList();
        sequences.Should().BeEquivalentTo([1, 2, 3, 4]);
    }

    [Fact]
    public void DealCommunityCards_Flop_DealsThreeCardsWithBurn()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        
        var results = engine.DealCommunityCards("Flop");
        
        // 1 burn + 3 community = 4 cards
        results.Should().HaveCount(4);
        results[0].CardType.Should().Be(DealCardType.BurnCard);
        results[0].Recipient.Should().Be("Burn");
        results.Skip(1).All(r => r.CardType == DealCardType.CommunityCard).Should().BeTrue();
        results.Skip(1).All(r => r.Recipient == "Community").Should().BeTrue();
    }

    [Fact]
    public void DealCommunityCards_Turn_DealsOneCardWithBurn()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        engine.DealCommunityCards("Flop");
        
        var results = engine.DealCommunityCards("Turn");
        
        // 1 burn + 1 community = 2 cards
        results.Should().HaveCount(2);
        results[0].CardType.Should().Be(DealCardType.BurnCard);
        results[1].CardType.Should().Be(DealCardType.CommunityCard);
    }

    [Fact]
    public void DealCommunityCards_River_DealsOneCardWithBurn()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        engine.DealHoleCards();
        engine.DealCommunityCards("Flop");
        engine.DealCommunityCards("Turn");
        
        var results = engine.DealCommunityCards("River");
        
        // 1 burn + 1 community = 2 cards
        results.Should().HaveCount(2);
        results[0].CardType.Should().Be(DealCardType.BurnCard);
        results[1].CardType.Should().Be(DealCardType.CommunityCard);
    }

    [Fact]
    public void DealCommunityCards_UnknownStreet_Throws()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var act = () => engine.DealCommunityCards("Unknown");
        
        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*Unknown street*");
    }

    [Fact]
    public void FullDealSequence_DealsCorrectTotalCards()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        // Deal hole cards: 4 cards (2×2)
        engine.DealHoleCards();
        // Flop: 1 burn + 3 community = 4
        engine.DealCommunityCards("Flop");
        // Turn: 1 burn + 1 community = 2
        engine.DealCommunityCards("Turn");
        // River: 1 burn + 1 community = 2
        engine.DealCommunityCards("River");
        
        // Total dealt: 4 + 4 + 2 + 2 = 12
        engine.CardsRemaining.Should().Be(52 - 12);
    }

    [Fact]
    public void DealToPlayer_DealsHoleCard()
    {
        var engine = new HoldEmDealingEngine();
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
        var engine = new HoldEmDealingEngine();
        engine.Initialize(TwoPlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var result = engine.DealToPlayer("Alice", faceUp: true);
        
        result.CardType.Should().Be(DealCardType.FaceUpCard);
    }

    [Fact]
    public void ReplayBySeed_Reproducible()
    {
        // First deal with seed
        var engine1 = new HoldEmDealingEngine();
        engine1.Initialize(TwoPlayers, dealerPosition: 0, seed: 42);
        engine1.Shuffle();
        var results1 = engine1.DealHoleCards();
        var flop1 = engine1.DealCommunityCards("Flop");
        
        // Second deal with same seed
        var engine2 = new HoldEmDealingEngine();
        engine2.Initialize(TwoPlayers, dealerPosition: 0, seed: 42);
        engine2.Shuffle();
        var results2 = engine2.DealHoleCards();
        var flop2 = engine2.DealCommunityCards("Flop");
        
        // Cards should be identical
        for (int i = 0; i < results1.Count; i++)
        {
            results1[i].Card.Should().Be(results2[i].Card);
        }
        
        for (int i = 0; i < flop1.Count; i++)
        {
            flop1[i].Card.Should().Be(flop2[i].Card);
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentCards()
    {
        var engine1 = new HoldEmDealingEngine();
        engine1.Initialize(TwoPlayers, dealerPosition: 0, seed: 1);
        engine1.Shuffle();
        var results1 = engine1.DealHoleCards();
        
        var engine2 = new HoldEmDealingEngine();
        engine2.Initialize(TwoPlayers, dealerPosition: 0, seed: 2);
        engine2.Shuffle();
        var results2 = engine2.DealHoleCards();
        
        // With different seeds, cards should be different (statistically very likely)
        var allCardsSame = results1.Zip(results2).All(pair => pair.First.Card == pair.Second.Card);
        allCardsSame.Should().BeFalse();
    }

    [Fact]
    public void DealHoleCards_AllCardsUnique()
    {
        var engine = new HoldEmDealingEngine();
        engine.Initialize(ThreePlayers, dealerPosition: 0);
        engine.Shuffle();
        
        var results = engine.DealHoleCards();
        
        var cards = results.Select(r => r.Card).ToList();
        cards.Should().OnlyHaveUniqueItems();
    }
}
