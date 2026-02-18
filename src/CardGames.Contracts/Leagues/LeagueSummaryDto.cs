namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueSummaryDto
{
	public required Guid LeagueId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public LeagueRole Role { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }
}