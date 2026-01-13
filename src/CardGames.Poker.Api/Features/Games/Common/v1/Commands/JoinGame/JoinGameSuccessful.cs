namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;

/// <summary>
/// Response for a successful join game operation.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="SeatIndex">The seat index the player was assigned to.</param>
/// <param name="PlayerId">The unique identifier of the player (for API calls).</param>
/// <param name="PlayerName">The name of the player who joined.</param>
/// <param name="PlayerAvatarUrl">The avatar URL of the player.</param>
/// <param name="PlayerFirstName">The first name of the player.</param>
/// <param name="CanPlayCurrentHand">Whether the player can participate in the current hand.</param>
public record JoinGameSuccessful(
	Guid GameId,
	int SeatIndex,
	Guid PlayerId,
	string PlayerName,
	string? PlayerAvatarUrl,
	string? PlayerFirstName,
	bool CanPlayCurrentHand);
