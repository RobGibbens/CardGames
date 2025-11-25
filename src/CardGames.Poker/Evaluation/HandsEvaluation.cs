using CardGames.Poker.Hands;
using CardGames.Poker.Hands.HandTypes;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Evaluation;

public static class HandsEvaluation
{
    public static IEnumerable<string> GetPlayers<THand>(IReadOnlyCollection<IDictionary<string, THand>> hands)
        where THand : HandBase
        => hands.First().Keys;

    public static IEnumerable<WinDistribution> GroupByWins<THand>(
        IReadOnlyCollection<IDictionary<string, THand>> hands)
        where THand : HandBase
    {
        var players = GetPlayers(hands);
        var winCounts = players.ToDictionary(p => p, _ => 0);
        var tieCounts = players.ToDictionary(p => p, _ => 0);

        foreach (var round in hands)
        {
            var maxStrength = round.Max(h => h.Value.Strength);
            var winners = round.Where(h => h.Value.Strength == maxStrength).ToList();

            if (winners.Count == 1)
            {
                winCounts[winners[0].Key]++;
            }
            else
            {
                foreach (var winner in winners)
                {
                    tieCounts[winner.Key]++;
                }
            }
        }

        return players
            .Select(player => new WinDistribution(player, winCounts[player], tieCounts[player], hands.Count))
            .OrderBy(d => d.Wins);
    }

    public static IDictionary<string, IEnumerable<TypeDistribution>> AllMadeHandDistributions<THand>(
        IReadOnlyCollection<IDictionary<string, THand>> hands)
        where THand : HandBase
        => GetPlayers(hands).ToDictionary(player => player, player => MadeHandDistributionOf(hands, player));

    public static IEnumerable<TypeDistribution> MadeHandDistributionOf<THand>(
        IReadOnlyCollection<IDictionary<string, THand>> hands,
        string name)
        where THand : HandBase
        => hands
            .Select(round => round[name].Type)
            .GroupBy(type => type)
            .Select(grp => new TypeDistribution(grp.Key, grp.Count(), hands.Count))
            .OrderByDescending(grp => grp.Occurrences);
}

public sealed record TypeDistribution(HandType Type, int Occurrences, int Total)
{
    public decimal Frequency => (decimal)Occurrences / Total;
}

public sealed record WinDistribution(string Name, int Wins, int Ties, int Total)
{
    public decimal Percentage => (decimal)Wins / Total;
    public decimal TiePercentage => (decimal)Ties / Total;
}