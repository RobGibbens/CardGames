using System.Collections.Generic;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class PotManagerTests
{
    [Fact]
    public void Constructor_CreatesSingleEmptyPot()
    {
        var potManager = new PotManager();

        potManager.TotalPotAmount.Should().Be(0);
        potManager.Pots.Should().HaveCount(1);
    }

    [Fact]
    public void AddContribution_AddsToPot()
    {
        var potManager = new PotManager();

        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        potManager.TotalPotAmount.Should().Be(200);
        potManager.GetPlayerContribution("Alice").Should().Be(100);
        potManager.GetPlayerContribution("Bob").Should().Be(100);
    }

    [Fact]
    public void AddContribution_MultipleFromSamePlayer_Accumulates()
    {
        var potManager = new PotManager();

        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Alice", 50);

        potManager.TotalPotAmount.Should().Be(150);
        potManager.GetPlayerContribution("Alice").Should().Be(150);
    }

    [Fact]
    public void Reset_ClearsPotAndContributions()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);

        potManager.Reset();

        potManager.TotalPotAmount.Should().Be(0);
        potManager.GetPlayerContribution("Alice").Should().Be(0);
    }

    [Fact]
    public void RemovePlayerEligibility_RemovesFromAllPots()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        potManager.RemovePlayerEligibility("Alice");

        potManager.Pots[0].EligiblePlayers.Should().NotContain("Alice");
        potManager.Pots[0].EligiblePlayers.Should().Contain("Bob");
    }

    [Fact]
    public void AwardPots_AwardsToSingleWinner()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        var payouts = potManager.AwardPots(players => new[] { "Alice" });

        payouts.Should().ContainKey("Alice");
        payouts["Alice"].Should().Be(200);
    }

    [Fact]
    public void AwardPots_SplitsTieEvenly()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        var payouts = potManager.AwardPots(players => new[] { "Alice", "Bob" });

        payouts["Alice"].Should().Be(100);
        payouts["Bob"].Should().Be(100);
    }

    [Fact]
    public void AwardPots_HandlesOddChip()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 51);
        potManager.AddContribution("Bob", 50);

        var payouts = potManager.AwardPots(players => new[] { "Alice", "Bob" });

        // One gets 51, one gets 50
        (payouts["Alice"] + payouts["Bob"]).Should().Be(101);
    }

    [Fact]
    public void CalculateSidePots_CreatesSidePotsForAllIn()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0), // All-in for 50
            new("Bob", 150), // Has chips remaining
        };

        // Alice is all-in for 50, Bob called with 50
        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        players[0].PlaceBet(50); // Make Alice all-in

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(150);
    }
}
