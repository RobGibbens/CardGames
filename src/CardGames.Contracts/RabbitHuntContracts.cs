namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// A single projected community card for Rabbit Hunt display.
/// </summary>
public sealed record RabbitHuntCardDto
{
    /// <summary>
    /// The community card.
    /// </summary>
    public required ShowdownCard Card { get; init; }

    /// <summary>
    /// The display deal order for the projected board.
    /// </summary>
    public int DealOrder { get; init; }

    /// <summary>
    /// Whether this card was already visible to all players before Rabbit Hunt.
    /// </summary>
    public bool WasAlreadyVisible { get; init; }

    /// <summary>
    /// Whether this card is the Klondike card.
    /// </summary>
    public bool IsKlondikeCard { get; init; }

    /// <summary>
    /// The street or phase where the card belongs.
    /// </summary>
    public string? DealtAtPhase { get; init; }
}

/// <summary>
/// Private Rabbit Hunt response for a single player.
/// </summary>
public sealed record GetRabbitHuntSuccessful
{
    /// <summary>
    /// The game identifier.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// The current hand number for which the Rabbit Hunt was requested.
    /// </summary>
    public int HandNumber { get; init; }

    /// <summary>
    /// The game type code.
    /// </summary>
    public required string GameTypeCode { get; init; }

    /// <summary>
    /// The fully projected community board in display order.
    /// </summary>
    public required IReadOnlyList<RabbitHuntCardDto> CommunityCards { get; init; }

    /// <summary>
    /// Cards that were already face-up before Rabbit Hunt.
    /// </summary>
    public required IReadOnlyList<RabbitHuntCardDto> PreviouslyVisibleCards { get; init; }

    /// <summary>
    /// Cards revealed only because the player requested Rabbit Hunt.
    /// </summary>
    public required IReadOnlyList<RabbitHuntCardDto> NewlyRevealedCards { get; init; }
}