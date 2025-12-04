namespace CardGames.Poker.Api.Features.Games.Domain.Enums;

/// <summary>
/// Current status of a poker game.
/// </summary>
public enum GameStatus
{
	/// <summary>Game created, waiting for players to join</summary>
	WaitingForPlayers,

	/// <summary>Enough players have joined, ready to start a hand</summary>
	ReadyToStart,

	/// <summary>A hand is currently in progress</summary>
	InProgress,

	/// <summary>Game has ended</summary>
	Completed,

	/// <summary>Game was cancelled</summary>
	Cancelled
}