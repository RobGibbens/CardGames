namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueResponse
{
	public required Guid LeagueId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public required string CreatedByUserId { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public LeagueRole MyRole { get; init; }
}