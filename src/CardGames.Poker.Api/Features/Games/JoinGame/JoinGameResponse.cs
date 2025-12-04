namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Response returned after successfully joining a game.
/// </summary>
public record JoinGameResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int Position,
	PlayerStatus Status
);