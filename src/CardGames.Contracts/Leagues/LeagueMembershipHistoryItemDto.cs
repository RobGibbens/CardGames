namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueMembershipHistoryItemDto
{
	public required Guid EventId { get; init; }

	public required Guid LeagueId { get; init; }

	public required string UserId { get; init; }

	public required string ActorUserId { get; init; }

	public LeagueMembershipHistoryEventType EventType { get; init; }

	public DateTimeOffset OccurredAtUtc { get; init; }
}
