namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueOneOffEventRequest
{
	public required string Name { get; init; }

	public DateTimeOffset ScheduledAtUtc { get; init; }

	public LeagueOneOffEventType EventType { get; init; }

	public string? Notes { get; init; }
}