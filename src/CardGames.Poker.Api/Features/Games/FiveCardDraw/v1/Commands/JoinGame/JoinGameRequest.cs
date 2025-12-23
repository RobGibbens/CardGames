namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.JoinGame;

/// <summary>
/// Request model for joining a game.
/// </summary>
/// <param name="SeatIndex">The zero-based seat index to occupy.</param>
/// <param name="StartingChips">The initial chip stack for the player. Defaults to 1000.</param>
public record JoinGameRequest(int SeatIndex, int StartingChips = 1000);
