namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Represents a player's final action/result in a completed hand.
/// </summary>
public enum PlayerFinalAction
{
    /// <summary>Player won the hand.</summary>
    Won = 0,
    
    /// <summary>Player lost the hand.</summary>
    Lost = 1,
    
    /// <summary>Player split the pot with other players.</summary>
    SplitPot = 2,
    
    /// <summary>Player folded before showdown.</summary>
    Folded = 3
}

/// <summary>
/// Represents a card that was visible at the end of a hand.
/// </summary>
public sealed record HandHistoryCardDto
{
    /// <summary>
    /// The rank of the card (e.g., "A", "K", "Q", "J", "10", "9", etc.).
    /// </summary>
    public string? Rank { get; init; }

    /// <summary>
    /// The suit of the card (e.g., "Hearts", "Diamonds", "Clubs", "Spades").
    /// </summary>
    public string? Suit { get; init; }
}

/// <summary>
/// Represents a player's result in a completed hand.
/// </summary>
public sealed record HandHistoryPlayerResultDto
{
    /// <summary>
    /// The unique identifier of the player.
    /// </summary>
    public Guid PlayerId { get; init; }

    /// <summary>
    /// The display name of the player.
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// The final action/result for this player.
    /// </summary>
    public PlayerFinalAction? FinalAction { get; init; }

    /// <summary>
    /// The net amount won or lost for this hand.
    /// Positive = won, negative = lost.
    /// Includes antes, blinds, and all bets.
    /// </summary>
    public int? NetAmount { get; init; }

    /// <summary>
    /// The cards visible at the end of the hand.
    /// Only populated for players who reached showdown.
    /// Null for folded players (cards not revealed).
    /// </summary>
    public IReadOnlyList<HandHistoryCardDto>? FinalVisibleCards { get; init; }

    /// <summary>
    /// The seat position of the player.
    /// </summary>
    public int? SeatPosition { get; init; }

    /// <summary>
    /// Whether the player reached showdown.
    /// </summary>
    public bool? ReachedShowdown { get; init; }

    /// <summary>
    /// A descriptive label for the result (e.g., "Won (Showdown)", "Folded (Turn)").
    /// </summary>
    public string? ResultLabel { get; init; }
}

/// <summary>
/// Represents a completed hand entry with all player results for expandable display.
/// </summary>
public sealed record HandHistoryEntryWithPlayersDto
{
    /// <summary>
    /// The unique identifier of the hand history record.
    /// </summary>
    public Guid HandId { get; init; }

    /// <summary>
    /// The 1-based hand number within the game.
    /// </summary>
    public int HandNumber { get; init; }

    /// <summary>
    /// The display name of the winner(s).
    /// </summary>
    public string? WinnerName { get; init; }

    /// <summary>
    /// The total pot amount won.
    /// </summary>
    public int TotalPotAmount { get; init; }

    /// <summary>
    /// The number of winners (1 for single winner, >1 for split pot).
    /// </summary>
    public int WinnerCount { get; init; } = 1;

    /// <summary>
    /// Description of the winning hand (e.g., "Full House, Aces over Kings").
    /// Null for win-by-fold.
    /// </summary>
    public string? WinningHandDescription { get; init; }

    /// <summary>
    /// Whether the hand was won because all other players folded.
    /// </summary>
    public bool? WonByFold { get; init; }

    /// <summary>
    /// The UTC timestamp when the hand completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// All players' results for this hand.
    /// Includes every player who was dealt into the hand.
    /// </summary>
    public IReadOnlyList<HandHistoryPlayerResultDto>? PlayerResults { get; init; }
}

/// <summary>
/// Contains hand history with per-player results for the expandable dashboard display.
/// </summary>
public sealed record HandHistoryWithPlayersListDto
{
    /// <summary>
    /// The list of hand history entries with player results, sorted newest-first.
    /// </summary>
    public IReadOnlyList<HandHistoryEntryWithPlayersDto>? Entries { get; init; }

    /// <summary>
    /// Total number of hands played in this game.
    /// </summary>
    public int TotalHands { get; init; }

    /// <summary>
    /// Whether there are more entries available beyond those returned.
    /// </summary>
    public bool? HasMore { get; init; }
}
