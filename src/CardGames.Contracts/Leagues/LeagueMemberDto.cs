namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueMemberDto
{
	public required string UserId { get; init; }

	public LeagueRole Role { get; init; }

	public bool IsActive { get; init; }

	public DateTimeOffset JoinedAtUtc { get; init; }

	public DateTimeOffset? LeftAtUtc { get; init; }
}