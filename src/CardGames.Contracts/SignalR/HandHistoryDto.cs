namespace CardGames.Contracts.SignalR;

/// <summary>
/// Represents a completed hand entry for the dashboard history display.
/// </summary>
public sealed record HandHistoryEntryDto
{
    /// <summary>
    /// The 1-based hand number within the game.
    /// </summary>
    public required int HandNumber { get; init; }

    /// <summary>
    /// The UTC timestamp when the hand completed.
    /// </summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// The display name of the winner (or "Split" if multiple winners).
    /// </summary>
    public required string WinnerName { get; init; }

    /// <summary>
    /// The amount won by the winner (or total pot in split scenarios).
    /// </summary>
    public required int AmountWon { get; init; }

    /// <summary>
    /// Description of the winning hand (e.g., "Full House").
    /// Null for win-by-fold.
    /// </summary>
    public string? WinningHandDescription { get; init; }

    /// <summary>
    /// Whether the hand was won because all others folded.
    /// </summary>
    public bool WonByFold { get; init; }

    /// <summary>
    /// Number of players who won (for split pot display).
    /// </summary>
    public int WinnerCount { get; init; } = 1;

    /// <summary>
    /// The current player's result label (e.g., "Won (Showdown)", "Folded (Turn)").
    /// </summary>
    public string? CurrentPlayerResultLabel { get; init; }

    /// <summary>
    /// The current player's net chip change for this hand.
    /// </summary>
    public int CurrentPlayerNetDelta { get; init; }

    /// <summary>
    /// Whether the current player won this hand.
    /// </summary>
    public bool CurrentPlayerWon { get; init; }
}

/// <summary>
/// Contains hand history for the dashboard flyout.
/// </summary>
public sealed record HandHistoryListDto
{
    /// <summary>
    /// The list of hand history entries, sorted newest-first.
    /// </summary>
    public required IReadOnlyList<HandHistoryEntryDto> Entries { get; init; }

    /// <summary>
    /// Total number of hands played in this game.
    /// </summary>
    public int TotalHands { get; init; }

    /// <summary>
    /// Whether there are more entries available beyond those returned.
    /// </summary>
    public bool HasMore { get; init; }
}
