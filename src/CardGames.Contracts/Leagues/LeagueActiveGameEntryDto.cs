namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueActiveGameEntryDto
{
	public required Guid GameId { get; init; }

	public required string Name { get; init; }

	public required string EventType { get; init; }

	public DateTimeOffset? StartedAt { get; init; }

	public string? CurrentPhase { get; init; }

	public int PlayerCount { get; init; }

	public string? CreatedByName { get; init; }

	public string? GameTypeName { get; init; }
}