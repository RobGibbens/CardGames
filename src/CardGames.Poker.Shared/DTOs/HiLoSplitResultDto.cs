namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Information about a Hi/Lo winner for display purposes.
/// </summary>
public record HiLoWinnerInfoDto(
    /// <summary>
    /// Name of the winning player.
    /// </summary>
    string PlayerName,

    /// <summary>
    /// Description of the winning hand.
    /// </summary>
    string? HandDescription = null,

    /// <summary>
    /// Amount won.
    /// </summary>
    int WinAmount = 0
);

/// <summary>
/// Information about a Hi/Lo split pot result.
/// </summary>
public record HiLoSplitResultDto
{
    /// <summary>
    /// Amount in the high pot.
    /// </summary>
    public int HighPotAmount { get; init; }

    /// <summary>
    /// Amount in the low pot.
    /// </summary>
    public int LowPotAmount { get; init; }

    /// <summary>
    /// Total pot amount.
    /// </summary>
    public int TotalPotAmount { get; init; }

    /// <summary>
    /// Winners of the high pot.
    /// </summary>
    public IReadOnlyList<HiLoWinnerInfoDto> HighWinners { get; init; } = [];

    /// <summary>
    /// Winners of the low pot.
    /// </summary>
    public IReadOnlyList<HiLoWinnerInfoDto> LowWinners { get; init; } = [];

    /// <summary>
    /// Whether no low hand qualified.
    /// </summary>
    public bool NoLowQualified { get; init; }

    /// <summary>
    /// Whether a player scooped the entire pot.
    /// </summary>
    public bool IsScooped { get; init; }

    /// <summary>
    /// Name of the player who scooped (if applicable).
    /// </summary>
    public string? ScoopedByPlayer { get; init; }

    /// <summary>
    /// The low qualifier (e.g., 8 for eight-or-better).
    /// </summary>
    public int LowQualifier { get; init; } = 8;
}
