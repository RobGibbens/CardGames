using System;
using System.Collections.Generic;
using System.Linq;
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
    public void AddContribution_NegativeAmount_Throws()
    {
        var potManager = new PotManager();

        var action = () => potManager.AddContribution("Alice", -1);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*negative*");
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

    [Fact]
    public void CalculateSidePots_IncludesFoldedPlayerContributions()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 50),
            new("Bob", 200),
            new("Charlie", 200),
        };

        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        players[0].PlaceBet(50);
        players[1].PlaceBet(100);
        players[2].PlaceBet(100);
        players[1].Fold();
        potManager.RemovePlayerEligibility("Bob");

        potManager.CalculateSidePots(players);

        potManager.Pots.Should().HaveCount(2);
        potManager.Pots[0].Amount.Should().Be(150);
        potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "Alice", "Charlie" });
        potManager.Pots[1].Amount.Should().Be(100);
        potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "Charlie" });
    }

    [Fact]
    public void AwardPotsSplit_SplitsPotBetweenSevensAndHighHand()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 50);

        var result = potManager.AwardPotsSplit(
            eligible => new[] { "Alice" },      // Alice has sevens
            eligible => new[] { "Bob" },        // Bob has best hand
            new[] { "Alice", "Bob" }
        );

        result.TotalPayouts.Should().ContainKey("Alice");
        result.TotalPayouts.Should().ContainKey("Bob");
        result.SevensWinners.Should().Contain("Alice");
        result.HighHandWinners.Should().Contain("Bob");
        result.SevensPoolRolledOver.Should().BeFalse();
        (result.TotalPayouts["Alice"] + result.TotalPayouts["Bob"]).Should().Be(100);
    }

    [Fact]
    public void AwardPotsSplit_RollsOverSevensPoolWhenNoWinners()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        var result = potManager.AwardPotsSplit(
            eligible => Enumerable.Empty<string>(), // No one has sevens
            eligible => new[] { "Bob" },            // Bob has best hand
            new[] { "Alice", "Bob" }
        );

        result.SevensWinners.Should().BeEmpty();
        result.HighHandWinners.Should().Contain("Bob");
        result.SevensPoolRolledOver.Should().BeTrue();
        result.TotalPayouts["Bob"].Should().Be(200);  // Bob gets entire pot
        result.SevensPayouts.Should().BeEmpty();
        result.HighHandPayouts["Bob"].Should().Be(200);
    }

    [Fact]
    public void AwardPotsSplit_SamePlayerWinsBothPools()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        var result = potManager.AwardPotsSplit(
            eligible => new[] { "Alice" },  // Alice has sevens
            eligible => new[] { "Alice" },  // Alice also has best hand
            new[] { "Alice", "Bob" }
        );

        result.SevensWinners.Should().Contain("Alice");
        result.HighHandWinners.Should().Contain("Alice");
        result.TotalPayouts["Alice"].Should().Be(200);  // Alice gets entire pot
        result.SevensPayouts["Alice"].Should().Be(100);
        result.HighHandPayouts["Alice"].Should().Be(100);
    }

    [Fact]
    public void AwardPotsSplit_MultipleSevensWinnersSplitHalf()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        var result = potManager.AwardPotsSplit(
            eligible => new[] { "Alice", "Bob" },  // Both have sevens
            eligible => new[] { "Charlie" },       // Charlie has best hand
            new[] { "Alice", "Bob", "Charlie" }
        );

        result.SevensWinners.Should().Contain("Alice");
        result.SevensWinners.Should().Contain("Bob");
        result.HighHandWinners.Should().Contain("Charlie");

        // Sevens pool is 150 (half of 300), split between Alice and Bob = 75 each
        result.SevensPayouts["Alice"].Should().Be(75);
        result.SevensPayouts["Bob"].Should().Be(75);

        // High hand pool is 150
        result.HighHandPayouts["Charlie"].Should().Be(150);

        (result.TotalPayouts["Alice"] + result.TotalPayouts["Bob"] + result.TotalPayouts["Charlie"]).Should().Be(300);
    }

    [Fact]
    public void AwardPotsSplit_OddChipGoesToHighHandPool()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 51);
        potManager.AddContribution("Bob", 50);

        var result = potManager.AwardPotsSplit(
            eligible => new[] { "Alice" },
            eligible => new[] { "Bob" },
            new[] { "Alice", "Bob" }
        );

        // Total pot = 101, sevens pool = 50, high hand pool = 51
        result.SevensPayouts["Alice"].Should().Be(50);
        result.HighHandPayouts["Bob"].Should().Be(51);
    }
}
