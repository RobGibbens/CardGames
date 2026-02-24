namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to league management clients when a new join request is submitted.
/// </summary>
public sealed record LeagueJoinRequestSubmittedDto
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
	/// The requester user identifier.
	/// </summary>
	public required string RequesterUserId { get; init; }

	/// <summary>
	/// The requester display name, when available.
	/// </summary>
	public string? RequesterDisplayName { get; init; }

	/// <summary>
	/// The UTC time when the join request was created.
	/// </summary>
	public required DateTimeOffset CreatedAtUtc { get; init; }

	/// <summary>
	/// The UTC time when the join request expires.
	/// </summary>
	public required DateTimeOffset ExpiresAtUtc { get; init; }
}