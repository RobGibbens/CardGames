namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueJoinPreviewDto
{
	public required Guid LeagueId { get; init; }

	public required string LeagueName { get; init; }

	public string? LeagueDescription { get; init; }

	public required string ManagerDisplayName { get; init; }

	public int ActiveMemberCount { get; init; }

	public required string JoinPolicy { get; init; }
}