namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Request to join an existing poker game.
/// </summary>
public record JoinGameRequest(
	string PlayerName,
	int? BuyIn = null
);