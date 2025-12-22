using System.Collections.Generic;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Result of the drop-or-stay phase.
/// </summary>
public class DropOrStayResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public bool AllDropped { get; init; }
	public bool SinglePlayerStayed { get; init; }
	public int StayingPlayerCount { get; init; }
	public IReadOnlyList<string> StayingPlayerNames { get; init; }
	public IReadOnlyList<string> DroppedPlayerNames { get; init; }
}