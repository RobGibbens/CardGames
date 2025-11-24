using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.Poker.Simulations.Draw;

/// <summary>
/// Contains the results of a Five Card Draw simulation.
/// </summary>
public class FiveCardDrawSimulationResult
{
    private readonly int _nrOfHands;

    public FiveCardDrawSimulationResult(int nrOfHands, IReadOnlyCollection<IDictionary<string, DrawHand>> hands)
    {
        _nrOfHands = nrOfHands;
        Hands = hands;
    }

    public IReadOnlyCollection<IDictionary<string, DrawHand>> Hands { get; }

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
