using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Playground.Simulations.Baseball;

/// <summary>
/// Contains the results of a Baseball simulation.
/// </summary>
public class BaseballSimulationResult
{
    private readonly int _nrOfHands;

    public BaseballSimulationResult(int nrOfHands, IReadOnlyCollection<IDictionary<string, BaseballHand>> hands)
    {
        _nrOfHands = nrOfHands;
        Hands = hands;
    }

    public IReadOnlyCollection<IDictionary<string, BaseballHand>> Hands { get; }

    public IEnumerable<string> GetPlayers => Hands.First().Keys;

    /// <summary>
    /// Groups results by wins, showing how often each player won.
    /// </summary>
    public IEnumerable<(string Name, int Wins, decimal WinPercentage)> GroupByWins()
        => Hands
            .Select(playerHands => playerHands.OrderByDescending(hand => hand.Value.Strength).First())
            .GroupBy(playerHands => playerHands.Key)
            .Select(grp => (grp.Key, grp.Count(), (decimal)grp.Count() / _nrOfHands))
            .OrderByDescending(res => res.Item2);

    /// <summary>
    /// Returns the distribution of hand types made by all players.
    /// </summary>
    public IDictionary<string, IEnumerable<(HandType Type, int Occurences, decimal Frequency)>> AllMadeHandDistributions()
        => GetPlayers
            .ToDictionary(player => player, MadeHandDistributionOf);

    /// <summary>
    /// Returns the distribution of hand types made by a specific player.
    /// </summary>
    public IEnumerable<(HandType Type, int Occurences, decimal Frequency)> MadeHandDistributionOf(string name)
        => Hands
            .Select(playerHands => playerHands[name].Type)
            .GroupBy(type => type)
            .Select(grp => (grp.Key, grp.Count(), (decimal)grp.Count() / _nrOfHands))
            .OrderByDescending(x => x.Item1);
}
