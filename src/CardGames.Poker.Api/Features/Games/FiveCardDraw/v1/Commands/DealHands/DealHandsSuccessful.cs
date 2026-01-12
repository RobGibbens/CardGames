using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Models;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;


/// <summary>
/// Represents a successful deal of cards to all players.
/// </summary>
public record DealHandsSuccessful
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public Guid GameId { get; init; }

    /// <summary>
    /// The current phase of the game after dealing.
    /// </summary>
    public required string CurrentPhase { get; init; }

    /// <summary>
    /// The current hand number being played.
    /// </summary>
    public int HandNumber { get; init; }

    /// <summary>
    /// The index of the current player who must act.
    /// </summary>
    public int CurrentPlayerIndex { get; init; }

    /// <summary>
    /// The name of the current player who must act.
    /// </summary>
    public string? CurrentPlayerName { get; init; }

    /// <summary>
    /// The cards dealt to each player.
    /// </summary>
    public required IReadOnlyList<PlayerDealtCards> PlayerHands { get; init; }
}