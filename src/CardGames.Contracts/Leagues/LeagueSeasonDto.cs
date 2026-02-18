namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueSeasonDto
{
	public required Guid SeasonId { get; init; }

	public required Guid LeagueId { get; init; }

	public required string Name { get; init; }

	public int? PlannedEventCount { get; init; }

	public DateTimeOffset? StartsAtUtc { get; init; }

	public DateTimeOffset? EndsAtUtc { get; init; }

	public LeagueSeasonStatus Status { get; init; }

	public required string CreatedByUserId { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }
}