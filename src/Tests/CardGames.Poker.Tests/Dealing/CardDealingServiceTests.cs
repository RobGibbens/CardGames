using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Dealing;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Dealing;

public class CardDealingServiceTests
{
    private static readonly List<string> TwoPlayers = ["Alice", "Bob"];
    private static readonly List<string> ThreePlayers = ["Alice", "Bob", "Charlie"];

    #region Factory Tests

    [Fact]
    public void CreateHoldEm_ReturnsCorrectService()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        
        service.VariantId.Should().Be("holdem");
        service.VisibilityProvider.Should().BeOfType<CommunityCardVisibilityProvider>();
    }

    [Fact]
    public void CreateOmaha_ReturnsCorrectService()
    {
        var service = CardDealingServiceFactory.CreateOmaha();
        
        service.VariantId.Should().Be("omaha");
        service.VisibilityProvider.Should().BeOfType<CommunityCardVisibilityProvider>();
    }

    [Fact]
    public void CreateSevenCardStud_ReturnsCorrectService()
    {
        var service = CardDealingServiceFactory.CreateSevenCardStud();
        
        service.VariantId.Should().Be("seven-card-stud");
        service.VisibilityProvider.Should().BeOfType<StudVisibilityProvider>();
    }

    [Theory]
    [InlineData("holdem", "holdem")]
    [InlineData("omaha", "omaha")]
    [InlineData("seven-card-stud", "seven-card-stud")]
    [InlineData("unknown", "holdem")] // Default to holdem
    public void Create_ReturnsCorrectVariant(string input, string expected)
    {
        var service = CardDealingServiceFactory.Create(input);
        
        service.VariantId.Should().Be(expected);
    }

    #endregion

    #region HoldEm Service Tests

    [Fact]
    public void HoldEm_DealHoleCards_ReturnsCorrectResult()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        
        var result = service.DealHoleCards();
        
        result.PhaseName.Should().Be("Hole Cards");
        result.DealtCards.Should().HaveCount(4); // 2 players × 2 cards
        result.DealtCards.All(c => c.CardType == DealCardType.HoleCard).Should().BeTrue();
        result.DealtCards.All(c => !c.IsFaceUp).Should().BeTrue();
        result.ReplaySeed.Should().Be(service.Seed);
    }

    [Fact]
    public void HoldEm_DealCommunityCards_Flop_ReturnsFourCards()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        service.DealHoleCards();
        
        var result = service.DealCommunityCards("Flop");
        
        result.PhaseName.Should().Be("Flop");
        result.DealtCards.Should().HaveCount(4); // 1 burn + 3 community
        result.BurnCards.Should().HaveCount(1);
    }

    [Fact]
    public void HoldEm_GetVisibleCards_HoleCards_OwnerSeesAll()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        
        var holeCardsResult = service.DealHoleCards();
        var aliceCards = holeCardsResult.DealtCards.Where(c => c.Recipient == "Alice").ToList();
        
        var aliceView = service.GetVisibleCards(aliceCards, "Alice");
        var bobView = service.GetVisibleCards(aliceCards, "Bob");
        
        // Alice should see all her cards (both hole cards visible to her)
        aliceView.Should().HaveCount(2);
        aliceView.Count(v => v.VisibleCard is not null).Should().Be(2);
        
        // Bob should not see Alice's hole cards
        bobView.Should().HaveCount(2);
        bobView.Count(v => v.VisibleCard is not null).Should().Be(0);
    }

    [Fact]
    public void HoldEm_GetVisibleCards_CommunityCards_EveryoneSees()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        service.DealHoleCards();
        
        var flopResult = service.DealCommunityCards("Flop");
        var communityCards = flopResult.DealtCards
            .Where(c => c.CardType == DealCardType.CommunityCard)
            .ToList();
        
        var aliceView = service.GetVisibleCards(communityCards, "Alice");
        var bobView = service.GetVisibleCards(communityCards, "Bob");
        
        // Both should see all community cards
        aliceView.All(v => v.VisibleCard != null).Should().BeTrue();
        bobView.All(v => v.VisibleCard != null).Should().BeTrue();
    }

    [Fact]
    public void HoldEm_BurnCards_AccumulateAcrossStreets()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        service.DealHoleCards();
        
        service.BurnCards.Should().BeEmpty();
        
        service.DealCommunityCards("Flop");
        service.BurnCards.Should().HaveCount(1);
        
        service.DealCommunityCards("Turn");
        service.BurnCards.Should().HaveCount(2);
        
        service.DealCommunityCards("River");
        service.BurnCards.Should().HaveCount(3);
    }

    [Fact]
    public void HoldEm_Shuffle_ResetsBurnCards()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        service.DealHoleCards();
        service.DealCommunityCards("Flop");
        
        service.Shuffle();
        
        service.BurnCards.Should().BeEmpty();
    }

    #endregion

    #region Stud Service Tests

    [Fact]
    public void Stud_DealHoleCards_ReturnsCorrectMixedVisibility()
    {
        var service = CardDealingServiceFactory.CreateSevenCardStud();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        
        var result = service.DealHoleCards();
        
        result.PhaseName.Should().Be("Hole Cards");
        // 2 players × 3 cards (2 hole + 1 door)
        result.DealtCards.Should().HaveCount(6);
        
        // Check card types
        var holeCards = result.DealtCards.Where(c => c.CardType == DealCardType.HoleCard).ToList();
        var faceUpCards = result.DealtCards.Where(c => c.CardType == DealCardType.FaceUpCard).ToList();
        
        holeCards.Should().HaveCount(4); // 2 per player
        faceUpCards.Should().HaveCount(2); // 1 per player
        
        // Visibility
        holeCards.All(c => !c.IsFaceUp).Should().BeTrue();
        faceUpCards.All(c => c.IsFaceUp).Should().BeTrue();
    }

    [Fact]
    public void Stud_GetVisibleCards_MixedVisibility()
    {
        var service = CardDealingServiceFactory.CreateSevenCardStud();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        
        var result = service.DealHoleCards();
        var aliceCards = result.DealtCards.Where(c => c.Recipient == "Alice").ToList();
        
        var aliceView = service.GetVisibleCards(aliceCards, "Alice");
        var bobView = service.GetVisibleCards(aliceCards, "Bob");
        
        // Alice has 3 cards: 2 hole + 1 face-up
        aliceCards.Should().HaveCount(3);
        
        // Alice should see all her own cards (both hole and face-up)
        aliceView.Should().HaveCount(3);
        aliceView.Count(v => v.VisibleCard is not null).Should().Be(3);
        
        // Bob should only see Alice's face-up card(s) - using 'is not null' pattern
        bobView.Should().HaveCount(3);
        bobView.Count(v => v.VisibleCard is not null).Should().Be(1); // 1 face-up card
        bobView.Count(v => v.VisibleCard is null).Should().Be(2); // 2 hole cards
        
        // The visible card should be the face-up one
        var visibleToBob = bobView.Where(v => v.VisibleCard is not null).ToList();
        visibleToBob.Should().HaveCount(1);
        visibleToBob.First().DealtCard.CardType.Should().Be(DealCardType.FaceUpCard);
    }

    #endregion

    #region DealToPlayer Tests

    [Fact]
    public void DealToPlayer_ReturnsCorrectVisibility()
    {
        var service = CardDealingServiceFactory.CreateHoldEm();
        service.Initialize(TwoPlayers, dealerPosition: 0);
        service.Shuffle();
        
        var holeCard = service.DealToPlayer("Alice", faceUp: false);
        var faceUpCard = service.DealToPlayer("Alice", faceUp: true);
        
        holeCard.IsFaceUp.Should().BeFalse();
        holeCard.CardType.Should().Be(DealCardType.HoleCard);
        
        faceUpCard.IsFaceUp.Should().BeTrue();
        faceUpCard.CardType.Should().Be(DealCardType.FaceUpCard);
    }

    #endregion
}
