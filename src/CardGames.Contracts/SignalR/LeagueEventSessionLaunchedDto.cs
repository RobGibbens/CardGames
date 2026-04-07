namespace CardGames.Contracts.SignalR;

/// <summary>
/// Indicates that a league event launched a new game session and connected viewers should refresh event state.
/// </summary>
public sealed record LeagueEventSessionLaunchedDto
{
	/// <summary>
	/// The league containing the launched event.
	/// </summary>
	public required Guid LeagueId { get; init; }

	/// <summary>
	/// The launched event identifier.
	/// </summary>
	public required Guid EventId { get; init; }

	/// <summary>
	/// The event source type.
	/// </summary>
	public required LeagueEventSourceType SourceType { get; init; }

	/// <summary>
	/// The season identifier when the event belongs to a season.
	/// </summary>
	public Guid? SeasonId { get; init; }

	/// <summary>
	/// The game created by the launch action.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The UTC time when the game was launched.
	/// </summary>
	public required DateTimeOffset LaunchedAtUtc { get; init; }
}

public enum LeagueEventSourceType
{
	Season = 1,
	OneOff = 2
}