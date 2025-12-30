using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Betting;

/// <summary>
/// Represents a pot or side pot in poker.
/// </summary>
public class Pot
{
    public int Amount { get; private set; }
    public HashSet<string> EligiblePlayers { get; }

    public Pot()
    {
        Amount = 0;
        EligiblePlayers = [];
    }

    public Pot(int amount, IEnumerable<string> eligiblePlayers)
    {
        Amount = amount;
        EligiblePlayers = eligiblePlayers.ToHashSet();
    }

    public void Add(int amount)
    {
        Amount += amount;
    }

    public void AddEligiblePlayer(string playerName)
    {
        EligiblePlayers.Add(playerName);
    }
}

/// <summary>
/// Manages the pot and side pots during a poker hand.
/// </summary>
public class PotManager
{
    private readonly List<Pot> _pots = [];
    private readonly Dictionary<string, int> _contributions = new();

    public int TotalPotAmount => _pots.Sum(p => p.Amount);

    public IReadOnlyList<Pot> Pots => _pots.AsReadOnly();

    public PotManager()
    {
        _pots.Add(new Pot());
    }

    /// <summary>
    /// Records a contribution from a player to the pot.
    /// </summary>
    public void AddContribution(string playerName, int amount)
    {
        if (!_contributions.TryAdd(playerName, amount))
        {
            _contributions[playerName] += amount;
        }

        if (_pots.Count == 0 || !_pots[0].EligiblePlayers.Contains(playerName))
        {
            _pots[0].AddEligiblePlayer(playerName);
        }

        _pots[0].Add(amount);
    }

    /// <summary>
    /// Gets the total contribution from a specific player.
    /// </summary>
    public int GetPlayerContribution(string playerName)
    {
        return _contributions.TryGetValue(playerName, out var amount) ? amount : 0;
    }

    /// <summary>
    /// Creates side pots when players go all-in for different amounts.
    /// Call this after a betting round where all-in occurred.
    /// </summary>
    public void CalculateSidePots(IEnumerable<PokerPlayer> players)
    {
        var activePlayers = players.Where(p => !p.HasFolded).ToList();
        if (activePlayers.Count < 2)
        {
            return;
        }

        // Get all distinct contribution levels from all-in players
        var allInLevels = activePlayers
            .Where(p => p.IsAllIn)
            .Select(p => _contributions.TryGetValue(p.Name, out var c) ? c : 0)
            .Where(c => c > 0)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (allInLevels.Count == 0)
        {
            return; // No side pots needed
        }

        // Recalculate pots from scratch
        var totalAmount = _pots.Sum(p => p.Amount);
        _pots.Clear();

        var previousLevel = 0;
        foreach (var level in allInLevels)
        {
            var potAmount = 0;
            var eligiblePlayers = new HashSet<string>();

            foreach (var player in activePlayers)
            {
                var contribution = _contributions.TryGetValue(player.Name, out var c) ? c : 0;
                if (contribution >= level)
                {
                    var contributionToThisPot = Math.Min(contribution, level) - previousLevel;
                    if (contributionToThisPot > 0)
                    {
                        potAmount += contributionToThisPot;
                        eligiblePlayers.Add(player.Name);
                    }
                }
            }

            if (potAmount > 0)
            {
                _pots.Add(new Pot(potAmount, eligiblePlayers));
            }

            previousLevel = level;
        }

        // Create final pot for remaining contributions
        var maxContribution = _contributions.Values.Max();
        if (maxContribution > previousLevel)
        {
            var finalPotAmount = 0;
            var eligibleForFinal = new HashSet<string>();

            foreach (var player in activePlayers)
            {
                var contribution = _contributions.TryGetValue(player.Name, out var c) ? c : 0;
                if (contribution > previousLevel)
                {
                    var contributionToFinal = contribution - previousLevel;
                    finalPotAmount += contributionToFinal;
                    eligibleForFinal.Add(player.Name);
                }
            }

            if (finalPotAmount > 0)
            {
                _pots.Add(new Pot(finalPotAmount, eligibleForFinal));
            }
        }

        // Ensure we haven't lost any money
        var calculatedTotal = _pots.Sum(p => p.Amount);
        if (calculatedTotal != totalAmount)
        {
            // If there's a discrepancy, adjust the last pot
            if (_pots.Count > 0)
            {
                _pots[^1].Add(totalAmount - calculatedTotal);
            }
        }
    }

    /// <summary>
    /// Resets the pot for a new hand.
    /// </summary>
    public void Reset()
    {
        _pots.Clear();
        _pots.Add(new Pot());
        _contributions.Clear();
    }

    /// <summary>
    /// Removes a player from eligibility (when they fold).
    /// </summary>
    public void RemovePlayerEligibility(string playerName)
    {
        foreach (var pot in _pots)
        {
            pot.EligiblePlayers.Remove(playerName);
        }
    }

    /// <summary>
    /// Awards pots to winners. Returns the total amount awarded to each player.
    /// </summary>
    public Dictionary<string, int> AwardPots(Func<IEnumerable<string>, IEnumerable<string>> determineWinners)
    {
        var payouts = new Dictionary<string, int>();

        foreach (var pot in _pots)
        {
            if (pot.Amount == 0 || pot.EligiblePlayers.Count == 0)
            {
                continue;
            }

            var winners = determineWinners(pot.EligiblePlayers).ToList();
            if (winners.Count == 0)
            {
                continue;
            }

            var share = pot.Amount / winners.Count;
            var remainder = pot.Amount % winners.Count;

            foreach (var winner in winners)
            {
                var payout = share;
                if (remainder > 0)
                {
                    payout++;
                    remainder--;
                }

                if (payouts.TryGetValue(winner, out var existing))
                {
                    payouts[winner] = existing + payout;
                }
                else
                {
                    payouts[winner] = payout;
                }
            }
        }

        return payouts;
    }

    /// <summary>
    /// Result of a split pot award for games with dual payouts (e.g., 7s half-pot rule).
    /// </summary>
    public class SplitPotResult
    {
        /// <summary>
        /// Total payouts per player (sevens + high hand combined).
        /// </summary>
        public Dictionary<string, int> TotalPayouts { get; init; } = [];

        /// <summary>
        /// Payouts from the sevens pool per player.
        /// </summary>
        public Dictionary<string, int> SevensPayouts { get; init; } = [];

        /// <summary>
        /// Payouts from the high hand pool per player.
        /// </summary>
        public Dictionary<string, int> HighHandPayouts { get; init; } = [];

        /// <summary>
        /// Player names who won the sevens pool.
        /// </summary>
        public List<string> SevensWinners { get; init; } = [];

        /// <summary>
        /// Player names who won the high hand pool.
        /// </summary>
        public List<string> HighHandWinners { get; init; } = [];

        /// <summary>
        /// Whether the sevens pool was rolled into the high hand pool because no one qualified.
        /// </summary>
        public bool SevensPoolRolledOver { get; set; }
    }

    /// <summary>
    /// Awards pots with dual-payout logic for split-pot games (e.g., Twos/Jacks/Axe with 7s half-pot rule).
    /// Each pot is split into a sevens pool (half) and a high hand pool (other half).
    /// If no players qualify for the sevens pool, it rolls into the high hand pool.
    /// </summary>
    /// <param name="determineSevensWinners">Function that returns players who have natural 7s from the eligible players.</param>
    /// <param name="determineHighHandWinners">Function that returns the best hand winner(s) from the eligible players.</param>
    /// <param name="orderedPlayers">Players ordered by seat position (dealer-left first) for deterministic remainder distribution.</param>
    /// <returns>Split pot result containing detailed payouts for both pools.</returns>
    public SplitPotResult AwardPotsSplit(
        Func<IEnumerable<string>, IEnumerable<string>> determineSevensWinners,
        Func<IEnumerable<string>, IEnumerable<string>> determineHighHandWinners,
        IReadOnlyList<string> orderedPlayers)
    {
        var result = new SplitPotResult();
        var orderedPlayersList = orderedPlayers.ToList();

        foreach (var pot in _pots)
        {
            if (pot.Amount == 0 || pot.EligiblePlayers.Count == 0)
            {
                continue;
            }

            // Split pot into two halves (high hand pool gets the odd chip)
            var sevensPool = pot.Amount / 2;
            var handPool = pot.Amount - sevensPool;

            // Determine sevens winners from eligible players
            var sevensWinnersList = determineSevensWinners(pot.EligiblePlayers)
                .Where(w => orderedPlayersList.Contains(w))
                .OrderBy(w => orderedPlayersList.IndexOf(w))
                .ToList();

            // If no sevens winners, roll the sevens pool into the hand pool
            if (sevensWinnersList.Count == 0)
            {
                handPool += sevensPool;
                sevensPool = 0;
                result.SevensPoolRolledOver = true;
            }

            // Determine high hand winners
            var handWinnersList = determineHighHandWinners(pot.EligiblePlayers)
                .Where(w => orderedPlayersList.Contains(w))
                .OrderBy(w => orderedPlayersList.IndexOf(w))
                .ToList();

            // Award sevens pool
            if (sevensPool > 0 && sevensWinnersList.Count > 0)
            {
                var share = sevensPool / sevensWinnersList.Count;
                var remainder = sevensPool % sevensWinnersList.Count;

                foreach (var winner in sevensWinnersList)
                {
                    var payout = share;
                    if (remainder > 0)
                    {
                        payout++;
                        remainder--;
                    }

                    AddToPayout(result.SevensPayouts, winner, payout);
                    AddToPayout(result.TotalPayouts, winner, payout);

                    if (!result.SevensWinners.Contains(winner))
                    {
                        result.SevensWinners.Add(winner);
                    }
                }
            }

            // Award high hand pool
            if (handPool > 0 && handWinnersList.Count > 0)
            {
                var share = handPool / handWinnersList.Count;
                var remainder = handPool % handWinnersList.Count;

                foreach (var winner in handWinnersList)
                {
                    var payout = share;
                    if (remainder > 0)
                    {
                        payout++;
                        remainder--;
                    }

                    AddToPayout(result.HighHandPayouts, winner, payout);
                    AddToPayout(result.TotalPayouts, winner, payout);

                    if (!result.HighHandWinners.Contains(winner))
                    {
                        result.HighHandWinners.Add(winner);
                    }
                }
            }
        }

        return result;
    }

    private static void AddToPayout(Dictionary<string, int> payouts, string player, int amount)
    {
        if (payouts.TryGetValue(player, out var existing))
        {
            payouts[player] = existing + amount;
        }
        else
        {
            payouts[player] = amount;
        }
    }
}
