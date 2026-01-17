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

        [Fact]
        public void CalculateSidePots_ThreePlayersAllInDifferentAmounts_CreatesThreePots()
        {
            // Scenario from bug report:
            // Rob: 1955 total contribution (5 ante + 1950 all-in)
            // Dean: 635 total contribution (5 ante + 630 all-in)
            // Lynne: 220 total contribution (5 ante + 215 all-in)
            var potManager = new PotManager();
            var players = new List<PokerPlayer>
            {
                new("Rob", 0),   // All-in (no chips remaining)
                new("Dean", 0),  // All-in (no chips remaining)
                new("Lynne", 0)  // All-in (no chips remaining)
            };

            // Simulate ante phase
            potManager.AddContribution("Rob", 5);
            potManager.AddContribution("Dean", 5);
            potManager.AddContribution("Lynne", 5);

            // Simulate betting - all go all-in
            potManager.AddContribution("Rob", 1950);
            potManager.AddContribution("Dean", 630);
            potManager.AddContribution("Lynne", 215);

            // Mark all players as all-in
            players[0].PlaceBet(1955); // Rob all-in
            players[1].PlaceBet(635);  // Dean all-in
            players[2].PlaceBet(220);  // Lynne all-in

            potManager.CalculateSidePots(players);

            // Total pot should equal sum of all contributions
            potManager.TotalPotAmount.Should().Be(2810);

            // Should create 3 pots
            potManager.Pots.Should().HaveCount(3);

            // Main pot: 220 × 3 = 660 (all three eligible)
            potManager.Pots[0].Amount.Should().Be(660);
            potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "Rob", "Dean", "Lynne" });

            // Side pot 1: (635-220) × 2 = 830 (Rob and Dean eligible)
            potManager.Pots[1].Amount.Should().Be(830);
            potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "Rob", "Dean" });

            // Side pot 2: (1955-635) × 1 = 1320 (only Rob eligible)
            potManager.Pots[2].Amount.Should().Be(1320);
            potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(new[] { "Rob" });
        }

        [Fact]
        public void AwardPots_SmallestStackWins_OnlyGetsMainPot()
        {
            // When Lynne wins with best hand, she should only get main pot (660)
            var potManager = new PotManager();
            var players = new List<PokerPlayer>
            {
                new("Rob", 0),
                new("Dean", 0),
                new("Lynne", 0)
            };

            potManager.AddContribution("Rob", 1955);
            potManager.AddContribution("Dean", 635);
            potManager.AddContribution("Lynne", 220);

            players[0].PlaceBet(1955);
            players[1].PlaceBet(635);
            players[2].PlaceBet(220);

            potManager.CalculateSidePots(players);

            // Simulate Lynne having best hand overall
            var payouts = potManager.AwardPots(eligiblePlayers =>
            {
                // Lynne has best hand, but she's only eligible for pots she contributed to
                if (eligiblePlayers.Contains("Lynne"))
                {
                    return new[] { "Lynne" };
                }
                // For pots where Lynne isn't eligible, Dean wins
                if (eligiblePlayers.Contains("Dean"))
                {
                    return new[] { "Dean" };
                }
                // Rob wins any pot where he's the only eligible player
                return new[] { "Rob" };
            });

            // Lynne should only get main pot
            payouts["Lynne"].Should().Be(660);

            // Dean should get side pot 1 (since Lynne can't win it and Dean beats Rob in this simulation)
            payouts["Dean"].Should().Be(830);

            // Rob should get side pot 2 (he's the only eligible player)
            payouts["Rob"].Should().Be(1320);

            // Total payouts should equal total pot
            payouts.Values.Sum().Should().Be(2810);
        }

        [Fact]
        public void CalculateSidePots_TwoPlayersAllInSameAmount_CreatesCorrectPots()
        {
            var potManager = new PotManager();
            var players = new List<PokerPlayer>
            {
                new("Alice", 0),  // All-in for 50
                new("Bob", 0),    // All-in for 50
                new("Charlie", 100), // Has chips remaining
                new("Diana", 100)    // Has chips remaining
            };

            potManager.AddContribution("Alice", 50);
            potManager.AddContribution("Bob", 50);
            potManager.AddContribution("Charlie", 100);
            potManager.AddContribution("Diana", 100);

            players[0].PlaceBet(50);
            players[1].PlaceBet(50);
            players[2].PlaceBet(100);
            players[3].PlaceBet(100);

            potManager.CalculateSidePots(players);

            // Total: 300
            potManager.TotalPotAmount.Should().Be(300);

            // Should create 2 pots
            potManager.Pots.Should().HaveCount(2);

            // Main pot: 50 × 4 = 200 (all four eligible)
            potManager.Pots[0].Amount.Should().Be(200);
            potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie", "Diana" });

            // Side pot: 50 × 2 = 100 (only Charlie and Diana)
            potManager.Pots[1].Amount.Should().Be(100);
            potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "Charlie", "Diana" });
        }

        [Fact]
        public void CalculateSidePots_AllPlayersAllIn_CreatesMultiplePots()
        {
            var potManager = new PotManager();
            var players = new List<PokerPlayer>
            {
                new("A", 0),  // All-in for 20
                new("B", 0),  // All-in for 50
                new("C", 0)   // All-in for 100
            };

            potManager.AddContribution("A", 20);
            potManager.AddContribution("B", 50);
            potManager.AddContribution("C", 100);

            players[0].PlaceBet(20);
            players[1].PlaceBet(50);
            players[2].PlaceBet(100);

            potManager.CalculateSidePots(players);

            // Total: 170
            potManager.TotalPotAmount.Should().Be(170);

            // Should create 3 pots
            potManager.Pots.Should().HaveCount(3);

            // Pot 1: 20 × 3 = 60
            potManager.Pots[0].Amount.Should().Be(60);
            potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "A", "B", "C" });

            // Pot 2: 30 × 2 = 60
            potManager.Pots[1].Amount.Should().Be(60);
            potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "B", "C" });

            // Pot 3: 50 × 1 = 50
            potManager.Pots[2].Amount.Should().Be(50);
            potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(new[] { "C" });
        }

        [Fact]
        public void CalculateSidePots_PlayerFoldsAfterAllIn_ContributionRemainsInPot()
        {
            var potManager = new PotManager();
            var players = new List<PokerPlayer>
            {
                new("Alice", 0),   // All-in for 50
                new("Bob", 100),   // Bets 100 then folds
                new("Charlie", 100) // Calls 100
            };

            potManager.AddContribution("Alice", 50);
            potManager.AddContribution("Bob", 100);
            potManager.AddContribution("Charlie", 100);

            players[0].PlaceBet(50);
            players[1].PlaceBet(100);
            players[2].PlaceBet(100);

            // Bob folds after betting
            players[1].Fold();
            potManager.RemovePlayerEligibility("Bob");

            potManager.CalculateSidePots(players);

            // Total: 250 (Bob's contribution stays in pot)
            potManager.TotalPotAmount.Should().Be(250);

            // Main pot: 50 × 3 = 150, but Bob is not eligible
            potManager.Pots[0].Amount.Should().Be(150);
            potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "Alice", "Charlie" });

            // Side pot: 50 × 2 = 100, but Bob is not eligible
            potManager.Pots[1].Amount.Should().Be(100);
            potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "Charlie" });
        }

            [Fact]
            public void CalculateSidePots_NoAllIns_DoesNotCreateSidePots()
            {
                var potManager = new PotManager();
                var players = new List<PokerPlayer>
                {
                    new("Alice", 100),
                    new("Bob", 100),
                    new("Charlie", 100)
                };

                potManager.AddContribution("Alice", 50);
                potManager.AddContribution("Bob", 50);
                potManager.AddContribution("Charlie", 50);

                // No one is all-in
                players[0].PlaceBet(50);
                players[1].PlaceBet(50);
                players[2].PlaceBet(50);

                potManager.CalculateSidePots(players);

                // Should remain as single pot
                potManager.Pots.Should().HaveCount(1);
                potManager.TotalPotAmount.Should().Be(150);
            }

            [Fact]
            public void CalculateSidePots_UserScenario_ThreePlayersAllInDifferentAmounts()
            {
                // Exact scenario from user bug report:
                // Hand #3: Dean (290 total = 5 ante + 285 all-in), Lynne (705 = 5 + 700), Rob (2005 = 5 + 2000)
                // Dean wins (best hand), Lynne second, Rob last
                // Expected: Dean gets Main pot (870), Lynne gets Side pot 1 (830), Rob gets Side pot 2 (1300)
                var potManager = new PotManager();
                var players = new List<PokerPlayer>
                {
                    new("Dean", 0),  // All-in for 290 total
                    new("Lynne", 0), // All-in for 705 total
                    new("Rob", 0)    // All-in for 2005 total
                };

                // Simulate contributions (ante + all-in)
                potManager.AddContribution("Dean", 290);   // 5 ante + 285 all-in
                potManager.AddContribution("Lynne", 705);  // 5 ante + 700 all-in
                potManager.AddContribution("Rob", 2005);   // 5 ante + 2000 all-in

                // Mark all as all-in by betting their full amounts
                players[0].PlaceBet(290);
                players[1].PlaceBet(705);
                players[2].PlaceBet(2005);

                potManager.CalculateSidePots(players);

                // Total pot should equal sum of all contributions
                potManager.TotalPotAmount.Should().Be(3000);

                // Should create 3 pots
                potManager.Pots.Should().HaveCount(3);

                // Main pot: 290 × 3 = 870 (Dean, Lynne, Rob all eligible)
                potManager.Pots[0].Amount.Should().Be(870);
                potManager.Pots[0].EligiblePlayers.Should().BeEquivalentTo(new[] { "Dean", "Lynne", "Rob" });

                // Side pot 1: (705-290) × 2 = 415 × 2 = 830 (Lynne and Rob eligible)
                potManager.Pots[1].Amount.Should().Be(830);
                potManager.Pots[1].EligiblePlayers.Should().BeEquivalentTo(new[] { "Lynne", "Rob" });

                // Side pot 2: (2005-705) × 1 = 1300 (only Rob eligible)
                potManager.Pots[2].Amount.Should().Be(1300);
                potManager.Pots[2].EligiblePlayers.Should().BeEquivalentTo(new[] { "Rob" });
            }

            [Fact]
            public void AwardPots_UserScenario_CorrectPayoutsToEachPlayer()
            {
                // Same setup as CalculateSidePots_UserScenario_ThreePlayersAllInDifferentAmounts
                // but also tests the award logic
                var potManager = new PotManager();
                var players = new List<PokerPlayer>
                {
                    new("Dean", 0),
                    new("Lynne", 0),
                    new("Rob", 0)
                };

                potManager.AddContribution("Dean", 290);
                potManager.AddContribution("Lynne", 705);
                potManager.AddContribution("Rob", 2005);

                players[0].PlaceBet(290);
                players[1].PlaceBet(705);
                players[2].PlaceBet(2005);

                potManager.CalculateSidePots(players);

                // Award pots with hand ranking: Dean > Lynne > Rob
                var payouts = potManager.AwardPots(eligiblePlayers =>
                {
                    // Return the best hand among eligible players
                    // Dean is best, but only wins pots he's eligible for
                    if (eligiblePlayers.Contains("Dean"))
                        return new[] { "Dean" };
                    if (eligiblePlayers.Contains("Lynne"))
                        return new[] { "Lynne" };
                    return new[] { "Rob" };
                });

                // Dean should win Main pot only (870)
                payouts["Dean"].Should().Be(870);

                // Lynne should win Side pot 1 (830) - she beats Rob but Dean isn't eligible
                payouts["Lynne"].Should().Be(830);

                // Rob should get Side pot 2 by default (1300) - he's the only eligible player
                payouts["Rob"].Should().Be(1300);

                // Total payouts should equal total pot
                payouts.Values.Sum().Should().Be(3000);
            }
        }
