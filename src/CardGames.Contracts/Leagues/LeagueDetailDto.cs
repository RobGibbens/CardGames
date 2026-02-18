namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueDetailDto
{
	public required Guid LeagueId { get; init; }

	public required string Name { get; init; }

	public string? Description { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public required string CreatedByUserId { get; init; }

	public string? CreatedByDisplayName { get; init; }

	public LeagueRole MyRole { get; init; }

	public int ActiveMemberCount { get; init; }
}