using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.History;

/// <summary>
/// Represents a complete summary of a poker hand upon completion.
/// This record captures all information needed for hand history display and auditing.
/// </summary>
/// <remarks>
/// <para>
/// A HandHistory is created exactly once when a hand completes (either by fold or showdown).
/// It captures the state at settlement time and should not be modified afterward.
/// </para>
/// <para>
/// The sum of all <see cref="PlayerResults"/> NetChipDelta values should equal zero
/// (or negative rake amount if rake is applied).
/// </para>
/// </remarks>
public sealed class HandHistory
{
    /// <summary>
    /// The unique identifier for this hand history record.
    /// </summary>
    public Guid HandId { get; }

    /// <summary>
    /// The unique identifier of the game session this hand belongs to.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// The 1-based sequence number of this hand within the game.
    /// </summary>
    public int HandNumber { get; }

    /// <summary>
    /// The UTC timestamp when this hand completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Indicates how the hand terminated.
    /// </summary>
    public HandEndReason EndReason { get; }

    /// <summary>
    /// The total pot size at settlement (sum of all contributions).
    /// </summary>
    public int TotalPot { get; }

    /// <summary>
    /// The rake amount taken from the pot, if applicable.
    /// </summary>
    public int Rake { get; }

    /// <summary>
    /// The list of winners and their winnings for this hand.
    /// </summary>
    public IReadOnlyList<HandWinner> Winners { get; }

    /// <summary>
    /// The list of per-player outcomes for all participants in this hand.
    /// </summary>
    public IReadOnlyList<HandPlayerResult> PlayerResults { get; }

    /// <summary>
    /// Optional description of the winning hand (e.g., "Full House, Aces over Kings").
    /// </summary>
    public string? WinningHandDescription { get; }

    /// <summary>
    /// Creates a new hand history record.
    /// </summary>
    public HandHistory(
        Guid handId,
        Guid gameId,
        int handNumber,
        DateTimeOffset completedAtUtc,
        HandEndReason endReason,
        int totalPot,
        int rake,
        IReadOnlyList<HandWinner> winners,
        IReadOnlyList<HandPlayerResult> playerResults,
        string? winningHandDescription = null)
    {
        ArgumentNullException.ThrowIfNull(winners);
        ArgumentNullException.ThrowIfNull(playerResults);

        if (handNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(handNumber), "Hand number must be at least 1.");
        }

        if (totalPot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPot), "Total pot cannot be negative.");
        }

        if (winners.Count == 0)
        {
            throw new ArgumentException("At least one winner is required.", nameof(winners));
        }

        HandId = handId;
        GameId = gameId;
        HandNumber = handNumber;
        CompletedAtUtc = completedAtUtc;
        EndReason = endReason;
        TotalPot = totalPot;
        Rake = rake;
        Winners = winners;
        PlayerResults = playerResults;
        WinningHandDescription = winningHandDescription;
    }

    /// <summary>
    /// Gets the result for a specific player, if they participated in this hand.
    /// </summary>
    /// <param name="playerId">The player's unique identifier.</param>
    /// <returns>The player's result, or null if they did not participate.</returns>
    public HandPlayerResult? GetPlayerResult(Guid playerId)
    {
        return PlayerResults.FirstOrDefault(r => r.PlayerId == playerId);
    }

    /// <summary>
    /// Gets a formatted display string for the winner(s).
    /// </summary>
    /// <returns>A string suitable for UI display, handling split pots.</returns>
    public string GetWinnerDisplay()
    {
        if (Winners.Count == 1)
        {
            return Winners[0].PlayerName;
        }

        return string.Join(", ", Winners.Select(w => w.PlayerName)) + " (Split)";
    }

    /// <summary>
    /// Gets the total amount won, which should equal TotalPot minus Rake.
    /// </summary>
    public int GetTotalWinnings() => Winners.Sum(w => w.AmountWon);
}
