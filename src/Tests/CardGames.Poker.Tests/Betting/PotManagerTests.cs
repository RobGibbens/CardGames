using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Shared.DTOs;
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

    #region Multi-All-In Edge Cases

    /// <summary>
    /// Classic scenario: Three players, one all-in short.
    /// Alice: 50 (all-in), Bob: 100, Charlie: 100
    /// Main pot: 50*3 = 150 (Alice, Bob, Charlie eligible)
    /// Side pot: 50*2 = 100 (Bob, Charlie eligible)
    /// </summary>
    [Fact]
    public void CalculateSidePots_ThreePlayers_OneAllInShort()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),   // All-in for 50
            new("Bob", 100),   // Has chips remaining  
            new("Charlie", 100) // Has chips remaining
        };

        // All contribute
        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        // Alice is all-in
        players[0].PlaceBet(50);

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(250);
        potManager.Pots.Should().HaveCount(2);

        // Main pot: 50 * 3 = 150
        potManager.Pots[0].Amount.Should().Be(150);
        potManager.Pots[0].EligiblePlayers.Should().Contain("Alice");
        potManager.Pots[0].EligiblePlayers.Should().Contain("Bob");
        potManager.Pots[0].EligiblePlayers.Should().Contain("Charlie");

        // Side pot: 50 * 2 = 100
        potManager.Pots[1].Amount.Should().Be(100);
        potManager.Pots[1].EligiblePlayers.Should().NotContain("Alice");
        potManager.Pots[1].EligiblePlayers.Should().Contain("Bob");
        potManager.Pots[1].EligiblePlayers.Should().Contain("Charlie");
    }

    /// <summary>
    /// Three players, two all-in at different levels.
    /// Alice: 30 (all-in), Bob: 70 (all-in), Charlie: 100
    /// Main pot: 30*3 = 90 (all eligible)
    /// Side pot 1: 40*2 = 80 (Bob, Charlie eligible)
    /// Side pot 2: 30 (Charlie only)
    /// </summary>
    [Fact]
    public void CalculateSidePots_ThreePlayers_TwoAllInDifferentLevels()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),    // All-in for 30
            new("Bob", 0),      // All-in for 70
            new("Charlie", 100) // Has chips remaining
        };

        potManager.AddContribution("Alice", 30);
        potManager.AddContribution("Bob", 70);
        potManager.AddContribution("Charlie", 100);

        // Make Alice and Bob all-in
        players[0].PlaceBet(30);
        players[1].PlaceBet(70);

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(200);
        potManager.Pots.Should().HaveCount(3);

        // Main pot: 30 * 3 = 90
        potManager.Pots[0].Amount.Should().Be(90);
        potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(["Alice", "Bob", "Charlie"]);

        // Side pot 1: 40 * 2 = 80 (70-30=40 from Bob and Charlie)
        potManager.Pots[1].Amount.Should().Be(80);
        potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(["Bob", "Charlie"]);

        // Side pot 2: 30 (100-70=30 from Charlie only)
        potManager.Pots[2].Amount.Should().Be(30);
        potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(["Charlie"]);
    }

    /// <summary>
    /// Four players with multiple side pots.
    /// Alice: 25 (all-in), Bob: 50 (all-in), Charlie: 75 (all-in), Dave: 100
    /// </summary>
    [Fact]
    public void CalculateSidePots_FourPlayers_MultipleSidePots()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),   // All-in for 25
            new("Bob", 0),     // All-in for 50
            new("Charlie", 0), // All-in for 75
            new("Dave", 100)   // Has chips remaining
        };

        potManager.AddContribution("Alice", 25);
        potManager.AddContribution("Bob", 50);
        potManager.AddContribution("Charlie", 75);
        potManager.AddContribution("Dave", 100);

        players[0].PlaceBet(25);
        players[1].PlaceBet(50);
        players[2].PlaceBet(75);

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(250);
        potManager.Pots.Should().HaveCount(4);

        // Main pot: 25 * 4 = 100
        potManager.Pots[0].Amount.Should().Be(100);
        potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(["Alice", "Bob", "Charlie", "Dave"]);

        // Side pot 1: 25 * 3 = 75
        potManager.Pots[1].Amount.Should().Be(75);
        potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(["Bob", "Charlie", "Dave"]);

        // Side pot 2: 25 * 2 = 50
        potManager.Pots[2].Amount.Should().Be(50);
        potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(["Charlie", "Dave"]);

        // Side pot 3: 25 (from Dave only)
        potManager.Pots[3].Amount.Should().Be(25);
        potManager.Pots[3].EligiblePlayers.Should().BeEquivalentTo(["Dave"]);
    }

    /// <summary>
    /// All players all-in at the same level - no side pots needed.
    /// </summary>
    [Fact]
    public void CalculateSidePots_AllPlayersAllInSameLevel_SinglePot()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),   // All-in for 100
            new("Bob", 0),     // All-in for 100
            new("Charlie", 0)  // All-in for 100
        };

        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        players[0].PlaceBet(100);
        players[1].PlaceBet(100);
        players[2].PlaceBet(100);

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(300);
        potManager.Pots.Should().HaveCount(1);
        potManager.Pots[0].Amount.Should().Be(300);
        potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(["Alice", "Bob", "Charlie"]);
    }

    /// <summary>
    /// Classic edge case: small, medium, and large stacks.
    /// Small: 100, Medium: 300, Large: 500
    /// </summary>
    [Fact]
    public void CalculateSidePots_SmallMediumLargeStacks()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Small", 0),   // All-in for 100
            new("Medium", 0),  // All-in for 300
            new("Large", 200)  // Has chips remaining (500 contributed)
        };

        potManager.AddContribution("Small", 100);
        potManager.AddContribution("Medium", 300);
        potManager.AddContribution("Large", 500);

        players[0].PlaceBet(100);
        players[1].PlaceBet(300);

        potManager.CalculateSidePots(players);

        potManager.TotalPotAmount.Should().Be(900);
        potManager.Pots.Should().HaveCount(3);

        // Main pot: 100 * 3 = 300
        potManager.Pots[0].Amount.Should().Be(300);
        potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(["Small", "Medium", "Large"]);

        // Side pot 1: 200 * 2 = 400
        potManager.Pots[1].Amount.Should().Be(400);
        potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(["Medium", "Large"]);

        // Side pot 2: 200 (Large only)
        potManager.Pots[2].Amount.Should().Be(200);
        potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(["Large"]);
    }

    /// <summary>
    /// Folded player should not be eligible for any pots.
    /// </summary>
    [Fact]
    public void CalculateSidePots_FoldedPlayerNotEligible()
    {
        var potManager = new PotManager();
        var alice = new PokerPlayer("Alice", 0);
        var bob = new PokerPlayer("Bob", 100);
        var charlie = new PokerPlayer("Charlie", 100);

        alice.PlaceBet(50); // All-in
        charlie.Fold(); // Charlie folds

        var players = new List<PokerPlayer> { alice, bob, charlie };

        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 50); // Contributed before folding

        potManager.CalculateSidePots(players);

        // Charlie should not be in eligible players (folded)
        foreach (var pot in potManager.Pots)
        {
            pot.EligiblePlayers.Should().NotContain("Charlie");
        }
    }

    /// <summary>
    /// Awards pots to different winners for main and side pots.
    /// Short stack wins main pot, bigger stack wins side pot.
    /// </summary>
    [Fact]
    public void AwardPots_DifferentWinnersForMainAndSidePots()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),   // All-in for 50 (wins main pot)
            new("Bob", 100),   // Has chips (wins side pot)
            new("Charlie", 100)
        };

        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        players[0].PlaceBet(50);

        potManager.CalculateSidePots(players);

        // Alice wins main pot, Bob wins side pot
        var payouts = potManager.AwardPots(eligiblePlayers =>
        {
            // For main pot (all three eligible), Alice wins
            // For side pot (Bob and Charlie eligible), Bob wins
            if (eligiblePlayers.Contains("Alice"))
                return new[] { "Alice" };
            return new[] { "Bob" };
        });

        payouts["Alice"].Should().Be(150); // Main pot
        payouts["Bob"].Should().Be(100);   // Side pot
    }

    #endregion

    #region DTO Tests

    [Fact]
    public void GetContributions_ReturnsAllPlayerContributions()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 50);
        potManager.AddContribution("Charlie", 75);

        var contributions = potManager.GetContributions();

        contributions.Should().HaveCount(3);
        contributions["Alice"].Should().Be(100);
        contributions["Bob"].Should().Be(50);
        contributions["Charlie"].Should().Be(75);
    }

    [Fact]
    public void GetBreakdown_ReturnsCorrectPotBreakdownDto()
    {
        var potManager = new PotManager();
        potManager.AddContribution("Alice", 100);
        potManager.AddContribution("Bob", 100);

        var breakdown = potManager.GetBreakdown();

        breakdown.TotalAmount.Should().Be(200);
        breakdown.Pots.Should().HaveCount(1);
        breakdown.Pots[0].Amount.Should().Be(200);
        breakdown.Pots[0].IsMainPot.Should().BeTrue();
        breakdown.Pots[0].EligiblePlayers.Should().Contain("Alice");
        breakdown.Pots[0].EligiblePlayers.Should().Contain("Bob");
        breakdown.PlayerContributions["Alice"].Should().Be(100);
        breakdown.PlayerContributions["Bob"].Should().Be(100);
    }

    [Fact]
    public void GetBreakdown_WithSidePots_ReturnsCorrectDtos()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Alice", 0),   // All-in for 50
            new("Bob", 100),   // Has chips
            new("Charlie", 100)
        };

        potManager.AddContribution("Alice", 50);
        potManager.AddContribution("Bob", 100);
        potManager.AddContribution("Charlie", 100);

        players[0].PlaceBet(50);

        potManager.CalculateSidePots(players);

        var breakdown = potManager.GetBreakdown();

        breakdown.TotalAmount.Should().Be(250);
        breakdown.Pots.Should().HaveCount(2);

        // Main pot
        breakdown.Pots[0].IsMainPot.Should().BeTrue();
        breakdown.Pots[0].Amount.Should().Be(150);

        // Side pot
        breakdown.Pots[1].IsMainPot.Should().BeFalse();
        breakdown.Pots[1].Amount.Should().Be(100);

        // Contributions
        breakdown.PlayerContributions.Should().HaveCount(3);
        breakdown.PlayerContributions["Alice"].Should().Be(50);
        breakdown.PlayerContributions["Bob"].Should().Be(100);
        breakdown.PlayerContributions["Charlie"].Should().Be(100);
    }

    [Fact]
    public void GetBreakdown_MultipleAllIns_ShowsAccurateContributions()
    {
        var potManager = new PotManager();
        var players = new List<PokerPlayer>
        {
            new("Small", 0),
            new("Medium", 0),
            new("Large", 200)
        };

        potManager.AddContribution("Small", 100);
        potManager.AddContribution("Medium", 300);
        potManager.AddContribution("Large", 500);

        players[0].PlaceBet(100);
        players[1].PlaceBet(300);

        potManager.CalculateSidePots(players);

        var breakdown = potManager.GetBreakdown();

        breakdown.TotalAmount.Should().Be(900);
        breakdown.PlayerContributions["Small"].Should().Be(100);
        breakdown.PlayerContributions["Medium"].Should().Be(300);
        breakdown.PlayerContributions["Large"].Should().Be(500);
    }

    #endregion
}
