namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueOneOffEventDto
{
	public required Guid EventId { get; init; }

	public required Guid LeagueId { get; init; }

	public required string Name { get; init; }

	public DateTimeOffset ScheduledAtUtc { get; init; }

	public LeagueOneOffEventType EventType { get; init; }

	public LeagueOneOffEventStatus Status { get; init; }

	public string? Notes { get; init; }

	public required string CreatedByUserId { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public Guid? LaunchedGameId { get; init; }
}