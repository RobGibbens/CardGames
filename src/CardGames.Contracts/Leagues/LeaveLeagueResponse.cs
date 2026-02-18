namespace CardGames.Poker.Api.Contracts;

public sealed record LeaveLeagueResponse
{
	public required Guid LeagueId { get; init; }

	public bool Left { get; init; }

	public bool WasActiveMember { get; init; }
}