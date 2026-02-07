namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Represents a successful start of a new hand in any poker game.
/// </summary>
public record StartHandSuccessful
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// The hand number that was started.
    /// </summary>
    public int HandNumber { get; init; }

    /// <summary>
    /// The current phase of the game after starting the hand.
    /// </summary>
    /// <remarks>
    /// This varies by game type. For example:
    /// - Five Card Draw: "CollectingAntes"
    /// - Kings and Lows: "Dealing"
    /// - Seven Card Stud: "CollectingAntes"
    /// </remarks>
    public required string CurrentPhase { get; init; }

    /// <summary>
    /// The number of active players in the hand.
    /// </summary>
    public int ActivePlayerCount { get; init; }
}
