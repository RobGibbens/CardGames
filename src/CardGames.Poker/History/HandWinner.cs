using System;

namespace CardGames.Poker.History;

/// <summary>
/// Records a winner of a poker hand and the amount won.
/// </summary>
/// <param name="PlayerId">The unique identifier of the winning player.</param>
/// <param name="PlayerName">The display name of the player at the time of the hand.</param>
/// <param name="AmountWon">The chip amount won by this player.</param>
public sealed record HandWinner(
    Guid PlayerId,
    string PlayerName,
    int AmountWon);
