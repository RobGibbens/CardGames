using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Playground.Simulations.Stud;

/// <summary>
/// Contains the results of a Follow the Queen simulation.
/// </summary>
public class FollowTheQueenSimulationResult
{
    private readonly int _nrOfHands;

    public FollowTheQueenSimulationResult(int nrOfHands, IReadOnlyCollection<IDictionary<string, FollowTheQueenHand>> hands)
    {
        _nrOfHands = nrOfHands;
        Hands = hands;
    }

    public IReadOnlyCollection<IDictionary<string, FollowTheQueenHand>> Hands { get; }

    public IEnumerable<string> GetPlayers => Hands.First().Keys;

    public IEnumerable<(string Name, int Wins, decimal WinPercentage)> GroupByWins()
        => Hands
            .Select(playerHands => playerHands.OrderByDescending(hand => hand.Value.Strength).First())
            .GroupBy(playerHands => playerHands.Key)
            .Select(grp => (grp.Key, grp.Count(), (decimal)grp.Count() / _nrOfHands))
            .OrderBy(res => res.Item2);

    public IDictionary<string, IEnumerable<(HandType Type, int Occurences, decimal Frequency)>> AllMadeHandDistributions()
        => GetPlayers
            .ToDictionary(player => player, MadeHandDistributionOf);

    public IEnumerable<(HandType Type, int Occurences, decimal Frequency)> MadeHandDistributionOf(string name)
        => Hands
            .Select(playerHands => playerHands[name].Type)
            .GroupBy(type => type)
            .Select(grp => (grp.Key, grp.Count(), (decimal)grp.Count() / _nrOfHands));
}
