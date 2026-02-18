namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueJoinRequestQueueItemDto
{
	public required Guid JoinRequestId { get; init; }

	public required Guid LeagueId { get; init; }

	public required Guid InviteId { get; init; }

	public required string RequesterUserId { get; init; }

	public required string RequesterDisplayName { get; init; }

	public LeagueJoinRequestStatus Status { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public DateTimeOffset ExpiresAtUtc { get; init; }
}
