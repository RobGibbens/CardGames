namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to lobby clients when a new game is created.
/// </summary>
public sealed record GameCreatedDto
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The friendly name of the game.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The game type identifier.
    /// </summary>
    public required Guid GameTypeId { get; init; }

    /// <summary>
    /// The game type code (e.g., "FiveCardDraw").
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
    /// The status of the game.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// When the game was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The ante amount for the game.
    /// </summary>
    public int Ante { get; init; }

    /// <summary>
    /// The minimum bet amount.
    /// </summary>
    public int MinBet { get; init; }

    /// <summary>
    /// The identifier of the user who created the game.
    /// </summary>
    public string? CreatedById { get; init; }

    /// <summary>
    /// The name of the user who created the game.
    /// </summary>
    public string? CreatedByName { get; init; }

    /// <summary>
    /// The metadata name for the game type.
    /// </summary>
    public string? GameTypeMetadataName { get; init; }

    /// <summary>
    /// The description of the game type.
    /// </summary>
    public string? GameTypeDescription { get; init; }

    /// <summary>
    /// The image name for the game type.
    /// </summary>
    public string? GameTypeImageName { get; init; }
}
