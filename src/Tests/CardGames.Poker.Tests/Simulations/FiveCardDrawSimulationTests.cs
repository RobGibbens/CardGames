using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Simulations.Draw;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Simulations;

public class FiveCardDrawSimulationTests
{
    [Fact]
    public void Simulation_Returns_Correct_Number_Of_Hands()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"));

        var result = simulation.Simulate(10);

        result.Hands.Should().HaveCount(10);
    }

    [Fact]
    public void Simulation_Returns_All_Players()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"))
            .WithPlayer(new FiveCardDrawPlayer("Charlie"));

        var result = simulation.Simulate(5);

        result.GetPlayers.Should().Contain(new[] { "Alice", "Bob", "Charlie" });
    }

    [Fact]
    public void Simulation_With_Given_Cards_Works()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice")
                .WithCards("Qh Kd As Ah Ad".ToCards()))
            .WithPlayer(new FiveCardDrawPlayer("Bob")
                .WithCards("2s 3h 4c 5d 6s".ToCards()));

        var result = simulation.Simulate(5);

        result.Hands.Should().HaveCount(5);
        result.GetPlayers.Should().HaveCount(2);
    }

    [Fact]
    public void Simulation_With_Partial_Cards_Works()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice")
                .WithCards("Qh Kd".ToCards()))
            .WithPlayer(new FiveCardDrawPlayer("Bob")
                .WithCards("As Ah Ad".ToCards()));

        var result = simulation.Simulate(5);

        result.Hands.Should().HaveCount(5);
        result.GetPlayers.Should().HaveCount(2);
    }

    [Fact]
    public void Simulation_With_Dead_Cards()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"))
            .WithDeadCards("As Ah Ad Ac".ToCards());

        var result = simulation.Simulate(10);

        result.Hands.Should().HaveCount(10);
    }

    [Fact]
    public void Simulation_Produces_Hand_Distribution()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"));

        var result = simulation.Simulate(100);

        var distribution = result.AllMadeHandDistributions();
        distribution.Should().ContainKey("Alice");
        distribution.Should().ContainKey("Bob");
    }

    [Fact]
    public void Simulation_GroupByWins_Returns_Results()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"));

        var result = simulation.Simulate(50);

        var wins = result.GroupByWins().ToList();
        wins.Should().NotBeEmpty();
        wins.Sum(w => w.Wins).Should().Be(50);
    }

    [Fact]
    public void Simulation_With_Three_Players()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice")
                .WithCards("Qh Kd".ToCards()))
            .WithPlayer(new FiveCardDrawPlayer("Bob")
                .WithCards("As Ah".ToCards()))
            .WithPlayer(new FiveCardDrawPlayer("Charlie")
                .WithCards("7c 7d".ToCards()));

        var result = simulation.Simulate(20);

        result.Hands.Should().HaveCount(20);
        result.GetPlayers.Should().HaveCount(3);
    }

    [Fact]
    public void Simulation_Each_Hand_Has_All_Players()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"));

        var result = simulation.Simulate(10);

        foreach (var hand in result.Hands)
        {
            hand.Should().ContainKey("Alice");
            hand.Should().ContainKey("Bob");
        }
    }

    [Fact]
    public void Simulation_Each_Player_Gets_Five_Cards()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer(new FiveCardDrawPlayer("Alice"))
            .WithPlayer(new FiveCardDrawPlayer("Bob"));

        var result = simulation.Simulate(10);

        foreach (var hand in result.Hands)
        {
            hand["Alice"].Cards.Should().HaveCount(5);
            hand["Bob"].Cards.Should().HaveCount(5);
        }
    }

    [Fact]
    public void Player_Builder_Pattern_Works_Correctly()
    {
        var player = new FiveCardDrawPlayer("TestPlayer")
            .WithCards("Ah Kh Qh Jh Th".ToCards());

        player.Name.Should().Be("TestPlayer");
        player.GivenCards.Should().HaveCount(5);
    }

    [Fact]
    public void Simulation_With_Name_And_Cards_Shorthand()
    {
        var simulation = new FiveCardDrawSimulation()
            .WithPlayer("Alice", "Ah Ad As Ac Kh".ToCards())
            .WithPlayer("Bob", "2s 3s 4s 5s 6s".ToCards());

        var result = simulation.Simulate(5);

        result.Hands.Should().HaveCount(5);
        result.GetPlayers.Should().HaveCount(2);
    }

    [Fact]
    public void Player_With_Too_Many_Cards_Throws()
    {
        var action = () => new FiveCardDrawPlayer("TestPlayer")
            .WithCards("Ah Kh Qh Jh Th 9h".ToCards());

        action.Should().Throw<System.ArgumentException>();
    }
}
