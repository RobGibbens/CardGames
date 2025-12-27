using System.ComponentModel.DataAnnotations;

namespace CardGames.Contracts.TableSettings;

/// <summary>
/// Request to update table settings.
/// </summary>
public sealed record UpdateTableSettingsRequest
{
    /// <summary>
    /// The display name of the table.
    /// </summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    /// <summary>
    /// The ante amount required from each player.
    /// </summary>
    [Range(0, 10000)]
    public int? Ante { get; init; }

    /// <summary>
    /// The minimum bet amount.
    /// </summary>
    [Range(1, 100000)]
    public int? MinBet { get; init; }

    /// <summary>
    /// The small blind amount (for blind-based games).
    /// </summary>
    [Range(1, 50000)]
    public int? SmallBlind { get; init; }

    /// <summary>
    /// The big blind amount (for blind-based games).
    /// </summary>
    [Range(1, 100000)]
    public int? BigBlind { get; init; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// Must match the current RowVersion of the table.
    /// </summary>
    [Required]
    public required string RowVersion { get; init; }
}
