namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueSeasonEventRequest
{
	public required string Name { get; init; }

	public int? SequenceNumber { get; init; }

	public DateTimeOffset? ScheduledAtUtc { get; init; }

	public string? Notes { get; init; }
}