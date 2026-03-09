using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class RazzHandTests
{
    [Fact]
    public void BestFiveLow_BeatsHigherLow()
    {
        var wheel = new RazzHand(
            "Ah 2d".ToCards(),
            "3c 4s 5h Kd".ToCards(),
            "Qc".ToCards());

        var sixLow = new RazzHand(
            "Ah 2d".ToCards(),
            "3c 4s 6h Kd".ToCards(),
            "Qc".ToCards());

        wheel.Strength.Should().BeGreaterThan(sixLow.Strength);
    }

    [Fact]
    public void Pairs_AreWorseThanNoPairLows()
    {
        var noPair = new RazzHand(
            "Ah 2d".ToCards(),
            "3c 4s 7h Kd".ToCards(),
            "Qc".ToCards());

        var paired = new RazzHand(
            "Ah 2d".ToCards(),
            "2c 4s 7h Kd".ToCards(),
            "Qc".ToCards());

        noPair.Strength.Should().BeGreaterThan(paired.Strength);
    }

    [Fact]
    public void StraightsAndFlushes_DoNotPenalizeLowHand()
    {
        var flushLike = new RazzHand(
            "Ah 2h".ToCards(),
            "3h 4h 8h Kd".ToCards(),
            "Qc".ToCards());

        var mixed = new RazzHand(
            "Ah 2d".ToCards(),
            "3c 4s 8h Kd".ToCards(),
            "Qc".ToCards());

        flushLike.Strength.Should().Be(mixed.Strength);
    }

    [Fact]
    public void LowHandDescription_UsesDescendingLowFormat()
    {
        var hand = new RazzHand(
            "Ah 2d".ToCards(),
            "3c 4s 6h Kd".ToCards(),
            "Qc".ToCards());

        var description = RazzHand.GetLowHandDescription(hand.GetBestLowHand());

        description.Should().Be("6-4-3-2-1 low");
    }
}
