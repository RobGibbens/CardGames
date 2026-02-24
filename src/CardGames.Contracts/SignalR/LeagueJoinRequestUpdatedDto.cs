namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to league management clients when a join request state changes.
/// </summary>
public sealed record LeagueJoinRequestUpdatedDto
{
	/// <summary>
	/// The league identifier for the join request.
	/// </summary>
	public required Guid LeagueId { get; init; }

	/// <summary>
	/// The join request identifier.
	/// </summary>
	public required Guid JoinRequestId { get; init; }

	/// <summary>
	/// The new join request status.
	/// </summary>
	public required string Status { get; init; }

	/// <summary>
	/// The UTC timestamp when the status was updated.
	/// </summary>
	public required DateTimeOffset UpdatedAtUtc { get; init; }
}
