using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Dealing;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Dealing;

public class CardVisibilityProviderTests
{
    private static readonly List<string> Players = ["Alice", "Bob", "Charlie"];

    #region CommunityCardVisibilityProvider Tests

    [Fact]
    public void CommunityCard_HoleCard_OnlyOwnerCanSee()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        var canAliceSee = provider.CanViewCard(DealCardType.HoleCard, "Alice", "Alice");
        var canBobSee = provider.CanViewCard(DealCardType.HoleCard, "Alice", "Bob");
        
        canAliceSee.Should().BeTrue();
        canBobSee.Should().BeFalse();
    }

    [Fact]
    public void CommunityCard_CommunityCard_EveryoneCanSee()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        var canAliceSee = provider.CanViewCard(DealCardType.CommunityCard, "Community", "Alice");
        var canBobSee = provider.CanViewCard(DealCardType.CommunityCard, "Community", "Bob");
        var canCharlieSee = provider.CanViewCard(DealCardType.CommunityCard, "Community", "Charlie");
        
        canAliceSee.Should().BeTrue();
        canBobSee.Should().BeTrue();
        canCharlieSee.Should().BeTrue();
    }

    [Fact]
    public void CommunityCard_BurnCard_NoOneCanSee()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        var canAliceSee = provider.CanViewCard(DealCardType.BurnCard, "Burn", "Alice");
        var canBobSee = provider.CanViewCard(DealCardType.BurnCard, "Burn", "Bob");
        
        canAliceSee.Should().BeFalse();
        canBobSee.Should().BeFalse();
    }

    [Fact]
    public void CommunityCard_GetVisibility_ReturnsCorrectForAllPlayers()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        var visibility = provider.GetVisibility(DealCardType.HoleCard, "Alice", Players);
        
        visibility.Should().HaveCount(3);
        visibility.First(v => v.PlayerName == "Alice").CanSeeCard.Should().BeTrue();
        visibility.First(v => v.PlayerName == "Bob").CanSeeCard.Should().BeFalse();
        visibility.First(v => v.PlayerName == "Charlie").CanSeeCard.Should().BeFalse();
    }

    [Fact]
    public void CommunityCard_HoleCard_IsFaceDown()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        provider.IsFaceUp(DealCardType.HoleCard).Should().BeFalse();
    }

    [Fact]
    public void CommunityCard_CommunityCard_IsFaceUp()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        provider.IsFaceUp(DealCardType.CommunityCard).Should().BeTrue();
    }

    [Fact]
    public void CommunityCard_BurnCard_IsFaceDown()
    {
        var provider = new CommunityCardVisibilityProvider("holdem");
        
        provider.IsFaceUp(DealCardType.BurnCard).Should().BeFalse();
    }

    #endregion

    #region StudVisibilityProvider Tests

    [Fact]
    public void Stud_HoleCard_OnlyOwnerCanSee()
    {
        var provider = new StudVisibilityProvider("seven-card-stud");
        
        var canAliceSee = provider.CanViewCard(DealCardType.HoleCard, "Alice", "Alice");
        var canBobSee = provider.CanViewCard(DealCardType.HoleCard, "Alice", "Bob");
        
        canAliceSee.Should().BeTrue();
        canBobSee.Should().BeFalse();
    }

    [Fact]
    public void Stud_FaceUpCard_EveryoneCanSee()
    {
        var provider = new StudVisibilityProvider("seven-card-stud");
        
        var canAliceSee = provider.CanViewCard(DealCardType.FaceUpCard, "Alice", "Alice");
        var canBobSee = provider.CanViewCard(DealCardType.FaceUpCard, "Alice", "Bob");
        var canCharlieSee = provider.CanViewCard(DealCardType.FaceUpCard, "Alice", "Charlie");
        
        canAliceSee.Should().BeTrue();
        canBobSee.Should().BeTrue();
        canCharlieSee.Should().BeTrue();
    }

    [Fact]
    public void Stud_GetVisibility_MixedCards_ReturnsCorrect()
    {
        var provider = new StudVisibilityProvider("seven-card-stud");
        
        // For face-up card, all should be able to see
        var faceUpVisibility = provider.GetVisibility(DealCardType.FaceUpCard, "Alice", Players);
        faceUpVisibility.All(v => v.CanSeeCard).Should().BeTrue();
        faceUpVisibility.All(v => v.IsFaceUp).Should().BeTrue();
        
        // For hole card, only owner should see
        var holeCardVisibility = provider.GetVisibility(DealCardType.HoleCard, "Alice", Players);
        holeCardVisibility.First(v => v.PlayerName == "Alice").CanSeeCard.Should().BeTrue();
        holeCardVisibility.First(v => v.PlayerName == "Bob").CanSeeCard.Should().BeFalse();
        holeCardVisibility.All(v => !v.IsFaceUp).Should().BeTrue();
    }

    [Fact]
    public void Stud_HoleCard_IsFaceDown()
    {
        var provider = new StudVisibilityProvider("seven-card-stud");
        
        provider.IsFaceUp(DealCardType.HoleCard).Should().BeFalse();
    }

    [Fact]
    public void Stud_FaceUpCard_IsFaceUp()
    {
        var provider = new StudVisibilityProvider("seven-card-stud");
        
        provider.IsFaceUp(DealCardType.FaceUpCard).Should().BeTrue();
    }

    #endregion

    #region Factory Tests

    [Theory]
    [InlineData("holdem", "CommunityCardVisibilityProvider")]
    [InlineData("texas-holdem", "CommunityCardVisibilityProvider")]
    [InlineData("omaha", "CommunityCardVisibilityProvider")]
    [InlineData("omaha-hi", "CommunityCardVisibilityProvider")]
    [InlineData("seven-card-stud", "StudVisibilityProvider")]
    [InlineData("stud", "StudVisibilityProvider")]
    [InlineData("razz", "StudVisibilityProvider")]
    public void Factory_ReturnsCorrectProviderType(string variantId, string expectedTypeName)
    {
        var provider = CardVisibilityProviderFactory.Create(variantId);
        
        provider.GetType().Name.Should().Be(expectedTypeName);
    }

    [Fact]
    public void Factory_UnknownVariant_ReturnsCommunityCardProvider()
    {
        var provider = CardVisibilityProviderFactory.Create("unknown-variant");
        
        provider.Should().BeOfType<CommunityCardVisibilityProvider>();
    }

    #endregion
}
