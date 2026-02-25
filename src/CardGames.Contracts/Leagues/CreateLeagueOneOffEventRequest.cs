namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueOneOffEventRequest
{
	public required string Name { get; init; }

	public DateTimeOffset ScheduledAtUtc { get; init; }

	public LeagueOneOffEventType EventType { get; init; }

	public string? Notes { get; init; }

	public string? GameTypeCode { get; init; }

	public string? TableName { get; init; }

	public int Ante { get; init; } = 10;

	public int MinBet { get; init; } = 20;
}