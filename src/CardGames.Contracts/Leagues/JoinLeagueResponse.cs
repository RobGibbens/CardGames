namespace CardGames.Poker.Api.Contracts;

public sealed record JoinLeagueResponse
{
	public required Guid LeagueId { get; init; }

	public bool Joined { get; init; }

	public bool AlreadyMember { get; init; }
}