namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueSeasonRequest
{
	public required string Name { get; init; }

	public int? PlannedEventCount { get; init; }

	public DateTimeOffset? StartsAtUtc { get; init; }

	public DateTimeOffset? EndsAtUtc { get; init; }
}