using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HandsEvaluationTests
{
    [Fact]
    public void GroupByWins_Correctly_Identifies_Single_Winner()
    {
        // Two players, one has better hand
        var board = "Kc Td 5s 3c 2h".ToCards();
        var player1Hand = new HoldemHand("As Ad".ToCards(), board); // Pair of Aces
        var player2Hand = new HoldemHand("Qs Qd".ToCards(), board); // Pair of Queens

        var hands = new List<IDictionary<string, HoldemHand>>
        {
            new Dictionary<string, HoldemHand>
            {
                { "Player1", player1Hand },
                { "Player2", player2Hand }
            }
        };

        var results = HandsEvaluation.GroupByWins(hands).ToList();

        var player1Result = results.First(r => r.Name == "Player1");
        var player2Result = results.First(r => r.Name == "Player2");

        player1Result.Wins.Should().Be(1);
        player1Result.Ties.Should().Be(0);
        player2Result.Wins.Should().Be(0);
        player2Result.Ties.Should().Be(0);
    }

    [Fact]
    public void GroupByWins_Correctly_Identifies_Tie()
    {
        // Two players with identical hand strength (same straight)
        // Board: 9c Td Jh Qc Kd - King high straight on board
        var board = "9c Td Jh Qc Kd".ToCards();
        var player1Hand = new HoldemHand("2s 3d".ToCards(), board); // Has King high straight from board
        var player2Hand = new HoldemHand("4s 5d".ToCards(), board); // Has King high straight from board

        var hands = new List<IDictionary<string, HoldemHand>>
        {
            new Dictionary<string, HoldemHand>
            {
                { "Player1", player1Hand },
                { "Player2", player2Hand }
            }
        };

        var results = HandsEvaluation.GroupByWins(hands).ToList();

        var player1Result = results.First(r => r.Name == "Player1");
        var player2Result = results.First(r => r.Name == "Player2");

        // Both should have 0 wins and 1 tie each
        player1Result.Wins.Should().Be(0);
        player1Result.Ties.Should().Be(1);
        player2Result.Wins.Should().Be(0);
        player2Result.Ties.Should().Be(1);
    }

    [Fact]
    public void GroupByWins_Handles_Multiple_Rounds_With_Mixed_Results()
    {
        // Create multiple rounds with different outcomes
        var board1 = "Kc Td 5s 3c 2h".ToCards();
        var board2 = "9c Td Jh Qc Kd".ToCards(); // Straight on board

        // Round 1: Player1 wins (Pair of Aces vs Pair of Queens)
        var round1 = new Dictionary<string, HoldemHand>
        {
            { "Player1", new HoldemHand("As Ad".ToCards(), board1) },
            { "Player2", new HoldemHand("Qs Qd".ToCards(), board1) }
        };

        // Round 2: Tie (both play the board straight)
        var round2 = new Dictionary<string, HoldemHand>
        {
            { "Player1", new HoldemHand("2s 3d".ToCards(), board2) },
            { "Player2", new HoldemHand("4s 5d".ToCards(), board2) }
        };

        // Round 3: Player2 wins (Ace-King vs low cards)
        var board3 = "7c 8d 5s 3c 2h".ToCards();
        var round3 = new Dictionary<string, HoldemHand>
        {
            { "Player1", new HoldemHand("9s Td".ToCards(), board3) },
            { "Player2", new HoldemHand("As Kd".ToCards(), board3) }
        };

        var hands = new List<IDictionary<string, HoldemHand>> { round1, round2, round3 };

        var results = HandsEvaluation.GroupByWins(hands).ToList();

        var player1Result = results.First(r => r.Name == "Player1");
        var player2Result = results.First(r => r.Name == "Player2");

        // Player1: 1 win, 1 tie
        player1Result.Wins.Should().Be(1);
        player1Result.Ties.Should().Be(1);

        // Player2: 1 win, 1 tie
        player2Result.Wins.Should().Be(1);
        player2Result.Ties.Should().Be(1);

        // Total should be 3
        player1Result.Total.Should().Be(3);
        player2Result.Total.Should().Be(3);
    }

    [Fact]
    public void GroupByWins_Handles_Three_Way_Tie()
    {
        // Three players all playing the same board straight
        var board = "9c Td Jh Qc Kd".ToCards(); // King high straight on board

        var round = new Dictionary<string, HoldemHand>
        {
            { "Player1", new HoldemHand("2s 3d".ToCards(), board) },
            { "Player2", new HoldemHand("4s 5d".ToCards(), board) },
            { "Player3", new HoldemHand("6s 7d".ToCards(), board) }
        };

        var hands = new List<IDictionary<string, HoldemHand>> { round };

        var results = HandsEvaluation.GroupByWins(hands).ToList();

        // All three should have 0 wins and 1 tie each
        foreach (var result in results)
        {
            result.Wins.Should().Be(0);
            result.Ties.Should().Be(1);
        }
    }

    [Fact]
    public void WinDistribution_Calculates_Percentages_Correctly()
    {
        var distribution = new WinDistribution("TestPlayer", 25, 10, 100);

        distribution.Percentage.Should().Be(0.25m);
        distribution.TiePercentage.Should().Be(0.10m);
    }
}
