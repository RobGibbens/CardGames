namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Status of a player in the game.
/// </summary>
public enum PlayerStatus
{
	Active,
	Folded,
	AllIn,
	SittingOut
}