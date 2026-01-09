namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;

/// <summary>
/// Response for the GET table settings query.
/// </summary>
public sealed record GetTableSettingsResponse
{
    /// <summary>
    /// The unique identifier of the game/table.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The display name of the table.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The game type code (e.g., "FIVECARDDRAW").
    /// </summary>
    public required string GameTypeCode { get; init; }

    /// <summary>
    /// The game type display name.
    /// </summary>
    public required string GameTypeName { get; init; }

    /// <summary>
    /// The current phase of the game.
    /// </summary>
    public required string CurrentPhase { get; init; }

    /// <summary>
    /// Whether the table settings can currently be edited.
    /// </summary>
    public bool IsEditable { get; init; }

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
    /// The maximum number of players allowed.
    /// </summary>
    public int MaxPlayers { get; init; }

    /// <summary>
    /// The minimum number of players required.
    /// </summary>
    public int MinPlayers { get; init; }

    /// <summary>
    /// The number of players currently seated.
    /// </summary>
    public int SeatedPlayerCount { get; init; }

    /// <summary>
    /// The unique identifier of the user who created this table.
    /// </summary>
    public string? CreatedById { get; init; }

    /// <summary>
    /// The name of the user who created this table.
    /// </summary>
    public string? CreatedByName { get; init; }

    /// <summary>
    /// When the table was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// The unique identifier of the user who last updated this table.
    /// </summary>
    public string? UpdatedById { get; init; }

    /// <summary>
    /// The name of the user who last updated this table.
    /// </summary>
    public string? UpdatedByName { get; init; }

    /// <summary>
    /// Concurrency token for optimistic locking.
    /// </summary>
    public required string RowVersion { get; init; }
}
