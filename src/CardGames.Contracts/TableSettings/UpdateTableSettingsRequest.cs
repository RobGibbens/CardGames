namespace CardGames.Contracts.TableSettings;

/// <summary>
/// Request to update table settings.
/// </summary>
public sealed record UpdateTableSettingsRequest
{
    /// <summary>
    /// The display name of the table.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The ante amount required from each player.
    /// </summary>
    public int? Ante { get; init; }

    /// <summary>
    /// The minimum bet amount.
    /// </summary>
    public int? MinBet { get; init; }

    /// <summary>
    /// The small blind amount (for blind-based games).
    /// </summary>
    public int? SmallBlind { get; init; }

    /// <summary>
    /// The big blind amount (for blind-based games).
    /// </summary>
    public int? BigBlind { get; init; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Must match the current RowVersion of the table.
    /// </summary>
    public required string RowVersion { get; init; }
}
