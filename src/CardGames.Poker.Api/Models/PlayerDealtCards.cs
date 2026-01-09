namespace CardGames.Poker.Api.Models;

/// <summary>
/// Represents the cards dealt to a player.
/// </summary>
public record PlayerDealtCards
{
    /// <summary>
    /// The name of the player who received the cards.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The seat position of the player.
    /// </summary>
    public int SeatPosition { get; init; }

    /// <summary>
    /// The cards dealt to this player.
    /// </summary>
    public required IReadOnlyList<DealtCard> Cards { get; init; }
}