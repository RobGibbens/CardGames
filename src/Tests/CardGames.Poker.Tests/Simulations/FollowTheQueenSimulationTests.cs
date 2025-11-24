using CardGames.Core.French.Cards.Extensions;
using CardGames.Playground.Simulations.Stud;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Simulations;

public class FollowTheQueenSimulationTests
{
    [Fact]
    public void Simulation_Returns_Correct_Number_Of_Hands()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"));

        var result = simulation.Simulate(10);

        result.Hands.Should().HaveCount(10);
    }

    [Fact]
    public void Simulation_Returns_All_Players()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"))
            .WithPlayer(new FollowTheQueenPlayer("Charlie"));

        var result = simulation.Simulate(5);

        result.GetPlayers.Should().Contain(new[] { "Alice", "Bob", "Charlie" });
    }

    [Fact]
    public void Simulation_With_Given_Hole_Cards_Works()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice")
                .WithHoleCards("Qh Kd".ToCards()))
            .WithPlayer(new FollowTheQueenPlayer("Bob")
                .WithHoleCards("As Ah".ToCards()));

        var result = simulation.Simulate(5);

        result.Hands.Should().HaveCount(5);
        result.GetPlayers.Should().HaveCount(2);
    }

    [Fact]
    public void Simulation_With_Given_Board_Cards_Works()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice")
                .WithBoardCards("5c 6d".ToCards()))
            .WithPlayer(new FollowTheQueenPlayer("Bob")
                .WithBoardCards("7h".ToCards()));

        var result = simulation.Simulate(5);

        result.Hands.Should().HaveCount(5);
    }

    [Fact]
    public void Simulation_With_Dead_Cards()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"))
            .WithDeadCards("As Ah Ad Ac".ToCards());

        var result = simulation.Simulate(10);

        result.Hands.Should().HaveCount(10);
    }

    [Fact]
    public void Simulation_Produces_Hand_Distribution()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"));

        var result = simulation.Simulate(100);

        var distribution = result.AllMadeHandDistributions();
        distribution.Should().ContainKey("Alice");
        distribution.Should().ContainKey("Bob");
    }

    [Fact]
    public void Simulation_GroupByWins_Returns_Results()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"));

        var result = simulation.Simulate(50);

        var wins = result.GroupByWins().ToList();
        wins.Should().NotBeEmpty();
        wins.Sum(w => w.Wins).Should().Be(50);
    }

    [Fact]
    public void Simulation_With_Three_Players()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice")
                .WithHoleCards("Qh Kd".ToCards()))
            .WithPlayer(new FollowTheQueenPlayer("Bob")
                .WithHoleCards("As Ah".ToCards()))
            .WithPlayer(new FollowTheQueenPlayer("Charlie")
                .WithHoleCards("7c 7d".ToCards()));

        var result = simulation.Simulate(20);

        result.Hands.Should().HaveCount(20);
        result.GetPlayers.Should().HaveCount(3);
    }

    [Fact]
    public void Simulation_Each_Hand_Has_All_Players()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"));

        var result = simulation.Simulate(10);

        foreach (var hand in result.Hands)
        {
            hand.Should().ContainKey("Alice");
            hand.Should().ContainKey("Bob");
        }
    }

    [Fact]
    public void Simulation_Each_Player_Gets_Seven_Cards()
    {
        var simulation = new FollowTheQueenSimulation()
            .WithPlayer(new FollowTheQueenPlayer("Alice"))
            .WithPlayer(new FollowTheQueenPlayer("Bob"));

        var result = simulation.Simulate(10);

        foreach (var hand in result.Hands)
        {
            hand["Alice"].Cards.Should().HaveCount(7);
            hand["Bob"].Cards.Should().HaveCount(7);
        }
    }

    [Fact]
    public void Player_Builder_Pattern_Works_Correctly()
    {
        var player = new FollowTheQueenPlayer("TestPlayer")
            .WithHoleCards("Ah Kh".ToCards())
            .WithBoardCards("Qh Jh Th".ToCards());

        player.Name.Should().Be("TestPlayer");
        player.GivenHoleCards.Should().HaveCount(2);
        player.GivenBoardCards.Should().HaveCount(3);
    }
}
