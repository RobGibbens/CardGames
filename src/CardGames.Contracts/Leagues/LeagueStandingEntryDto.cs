namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueStandingEntryDto
{
	public required string UserId { get; init; }

	public string? UserDisplayName { get; init; }

	public int Rank { get; init; }

	public int TotalEvents { get; init; }

	public int TotalPoints { get; init; }

	public int TotalChipsDelta { get; init; }

	public int? LastPlacement { get; init; }

	public DateTimeOffset UpdatedAtUtc { get; init; }
}
