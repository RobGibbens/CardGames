namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueInviteRequest
{
	public DateTimeOffset ExpiresAtUtc { get; init; }
}