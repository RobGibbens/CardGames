using System.Linq;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableCanvasFlyingCardPrivacyTests
{
    [Fact]
    public void FlyingCardInfo_DoesNotExposeFaceData()
    {
        var propertyNames = typeof(TableCanvas.FlyingCardInfo)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        propertyNames.Should().Equal("CardIndex", "TargetSeatIndex");
    }

    [Fact]
    public void FlyingCardInfo_PreservesAnimationCoordinates()
    {
        var flyingCard = new TableCanvas.FlyingCardInfo(TargetSeatIndex: 3, CardIndex: 1);

        flyingCard.TargetSeatIndex.Should().Be(3);
        flyingCard.CardIndex.Should().Be(1);
    }
}