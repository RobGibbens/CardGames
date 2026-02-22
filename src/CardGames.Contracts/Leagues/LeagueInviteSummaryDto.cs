namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueInviteSummaryDto
{
	public required Guid InviteId { get; init; }

	public required Guid LeagueId { get; init; }

	public LeagueInviteStatus Status { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public DateTimeOffset ExpiresAtUtc { get; init; }

	public string? InviteCode { get; init; }
}