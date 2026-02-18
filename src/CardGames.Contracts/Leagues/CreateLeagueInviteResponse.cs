namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueInviteResponse
{
	public required Guid InviteId { get; init; }

	public required Guid LeagueId { get; init; }

	public required string InviteUrl { get; init; }

	public DateTimeOffset ExpiresAtUtc { get; init; }

	public LeagueInviteStatus Status { get; init; }
}