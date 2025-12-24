namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.JoinGame;

/// <summary>
/// Response for a successful join game operation.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="SeatIndex">The seat index the player was assigned to.</param>
/// <param name="PlayerName">The name of the player who joined.</param>
/// <param name="CanPlayCurrentHand">Whether the player can participate in the current hand.</param>
public record JoinGameSuccessful(
    Guid GameId,
    int SeatIndex,
    string PlayerName,
    string? PlayerAvatarUrl,
    string? PlayerFirstName,
	bool CanPlayCurrentHand);
