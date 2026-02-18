namespace CardGames.Poker.Api.Contracts;

public sealed record JoinLeagueResponse
{
	public required Guid LeagueId { get; init; }

	public Guid? JoinRequestId { get; init; }

	public LeagueJoinRequestStatus? JoinRequestStatus { get; init; }

	public bool RequestSubmitted { get; init; }

	public bool Joined { get; init; }

	public bool AlreadyMember { get; init; }
}