using CardGames.Poker.Api.Contracts;

namespace CardGames.Contracts.SignalR;

/// <summary>
/// Private state sent only to the requesting player.
/// Contains their face-up cards and available actions.
/// </summary>
public sealed record PrivateStateDto
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The player's name/identifier.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The player's seat position.
    /// </summary>
    public int SeatPosition { get; init; }

    /// <summary>
    /// The player's hand with cards face-up.
    /// </summary>
    public required IReadOnlyList<CardPrivateDto> Hand { get; init; }

    /// <summary>
    /// A human-readable evaluation of the player's current hand (e.g., "Straight to the Ten").
    /// Only sent to the requesting player.
    /// </summary>
    public string? HandEvaluationDescription { get; init; }

    /// <summary>
    /// Available betting actions when it is the player's turn.
    /// Null if not the player's turn.
    /// </summary>
    public AvailableActionsDto? AvailableActions { get; init; }

    /// <summary>
    /// Draw phase information when in draw phase.
    /// </summary>
    public DrawPrivateDto? Draw { get; init; }

        /// <summary>
        /// Whether it is currently this player's turn to act.
        /// </summary>
        public bool IsMyTurn { get; init; }

        /// <summary>
        /// Hand history entries personalized for this player.
        /// Contains the player's result label and chip delta for each hand.
        /// </summary>
        public IReadOnlyList<HandHistoryEntryDto>? HandHistory { get; init; }
    }

/// <summary>
/// Private card representation with full card details visible.
/// </summary>
public sealed record CardPrivateDto
{
    /// <summary>
    /// The rank of the card (e.g., "A", "K", "10").
    /// </summary>
    public required string Rank { get; init; }

    /// <summary>
    /// The suit of the card (e.g., "Hearts", "Spades").
    /// </summary>
    public required string Suit { get; init; }

    /// <summary>
    /// The order in which this card was dealt.
    /// </summary>
    public int DealOrder { get; init; }

    /// <summary>
    /// Whether this card is selected for discard (during draw phase).
    /// </summary>
    public bool IsSelectedForDiscard { get; init; }
}

/// <summary>
/// Available betting actions for the current player.
/// </summary>
public sealed record AvailableActionsDto
{
    /// <summary>
    /// Whether the player can fold.
    /// </summary>
    public bool CanFold { get; init; }

    /// <summary>
    /// Whether the player can check.
    /// </summary>
    public bool CanCheck { get; init; }

    /// <summary>
    /// Whether the player can call.
    /// </summary>
    public bool CanCall { get; init; }

    /// <summary>
    /// Whether the player can bet.
    /// </summary>
    public bool CanBet { get; init; }

    /// <summary>
    /// Whether the player can raise.
    /// </summary>
    public bool CanRaise { get; init; }

    /// <summary>
    /// Whether the player can go all-in.
    /// </summary>
    public bool CanAllIn { get; init; }

    /// <summary>
    /// The minimum bet amount allowed.
    /// </summary>
    public int MinBet { get; init; }

    /// <summary>
    /// The maximum bet amount allowed.
    /// </summary>
    public int MaxBet { get; init; }

    /// <summary>
    /// The amount required to call.
    /// </summary>
    public int CallAmount { get; init; }

    /// <summary>
    /// The minimum raise amount.
    /// </summary>
    public int MinRaise { get; init; }
}

/// <summary>
/// Draw phase information for the player.
/// </summary>
public sealed record DrawPrivateDto
{
    /// <summary>
    /// Whether it is the player's turn to draw.
    /// </summary>
    public bool IsMyTurnToDraw { get; init; }

    /// <summary>
    /// Maximum number of cards the player can discard.
    /// </summary>
    public int MaxDiscards { get; init; }

    /// <summary>
    /// Whether the player has already drawn this round.
    /// </summary>
    public bool HasDrawnThisRound { get; init; }
}
