namespace CardGames.Poker.Api.Services;

/// <summary>
/// Parameters for recording a hand history entry.
/// </summary>
public sealed class RecordHandHistoryParameters
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The hand number (1-based).
    /// </summary>
    public required int HandNumber { get; init; }

    /// <summary>
    /// The UTC timestamp when the hand completed.
    /// </summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// Whether the hand was won by fold (all others folded).
    /// </summary>
    public required bool WonByFold { get; init; }

    /// <summary>
    /// The total pot amount at settlement.
    /// </summary>
    public required int TotalPot { get; init; }

    /// <summary>
    /// Optional description of the winning hand.
    /// </summary>
    public string? WinningHandDescription { get; init; }

    /// <summary>
    /// The list of winners and their amounts.
    /// </summary>
    public required IReadOnlyList<WinnerInfo> Winners { get; init; }

    /// <summary>
    /// The list of all player results for this hand.
    /// </summary>
    public required IReadOnlyList<PlayerResultInfo> PlayerResults { get; init; }
}

/// <summary>
/// Information about a winner.
/// </summary>
public sealed class WinnerInfo
{
    /// <summary>
    /// The player's database identifier.
    /// </summary>
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// The player's display name at hand time.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The amount won.
    /// </summary>
    public required int AmountWon { get; init; }
}

/// <summary>
/// Information about a player's result in a hand.
/// </summary>
public sealed class PlayerResultInfo
{
    /// <summary>
    /// The player's database identifier.
    /// </summary>
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// The player's display name at hand time.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The player's seat position.
    /// </summary>
    public required int SeatPosition { get; init; }

    /// <summary>
    /// Whether the player folded.
    /// </summary>
    public bool HasFolded { get; init; }

    /// <summary>
    /// Whether the player reached showdown.
    /// </summary>
    public bool ReachedShowdown { get; init; }

    /// <summary>
    /// Whether the player won (or split).
    /// </summary>
    public bool IsWinner { get; init; }

    /// <summary>
    /// Whether the pot was split among multiple winners.
    /// </summary>
    public bool IsSplitPot { get; init; }

    /// <summary>
    /// The net chip change for this player.
    /// </summary>
    public required int NetChipDelta { get; init; }

    /// <summary>
    /// Whether the player went all-in.
    /// </summary>
    public bool WentAllIn { get; init; }

    /// <summary>
    /// The betting street at which the player folded (null if didn't fold).
    /// </summary>
    public string? FoldStreet { get; init; }

    /// <summary>
    /// The final visible cards for this player.
    /// Should only be set for players who reached showdown.
    /// Cards are stored as a list of card codes (e.g., ["As", "Kh", "Qd"]).
    /// </summary>
    public IReadOnlyList<string>? FinalVisibleCards { get; init; }
}

/// <summary>
/// Service for recording hand history upon hand completion.
/// </summary>
public interface IHandHistoryRecorder
{
    /// <summary>
    /// Records a hand history entry for a completed hand.
    /// This operation is idempotent; recording the same GameId + HandNumber twice
    /// will not create duplicate entries.
    /// </summary>
    /// <param name="parameters">The hand history parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a new record was created, false if it already existed.</returns>
    Task<bool> RecordHandHistoryAsync(RecordHandHistoryParameters parameters, CancellationToken cancellationToken = default);
}
