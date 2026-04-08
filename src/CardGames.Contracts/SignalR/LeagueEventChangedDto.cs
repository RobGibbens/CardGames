namespace CardGames.Contracts.SignalR;

/// <summary>
/// Indicates that a league event changed and connected viewers should refresh league state.
/// </summary>
public sealed record LeagueEventChangedDto
{
	/// <summary>
	/// The affected league identifier.
	/// </summary>
	public required Guid LeagueId { get; init; }

	/// <summary>
	/// The affected event identifier.
	/// </summary>
	public required Guid EventId { get; init; }

	/// <summary>
	/// The source type for the affected event.
	/// </summary>
	public required LeagueEventSourceType SourceType { get; init; }

	/// <summary>
	/// The season identifier when the event belongs to a season.
	/// </summary>
	public Guid? SeasonId { get; init; }

	/// <summary>
	/// The type of change that occurred.
	/// </summary>
	public required LeagueEventChangeKind ChangeKind { get; init; }

	/// <summary>
	/// The UTC time when the change happened.
	/// </summary>
	public required DateTimeOffset ChangedAtUtc { get; init; }
}

public enum LeagueEventChangeKind
{
	Created = 1,
	Updated = 2,
	Deleted = 3,
	ResultsRecorded = 4
}