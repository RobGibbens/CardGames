using CardGames.Poker.Api.Features.Games.JoinGame;

namespace CardGames.Poker.Api.Features.Games.GetGameState;

/// <summary>
/// State of a player in the game (for public viewing).
/// </summary>
public record PlayerStateResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int Position,
	PlayerStatus Status
);