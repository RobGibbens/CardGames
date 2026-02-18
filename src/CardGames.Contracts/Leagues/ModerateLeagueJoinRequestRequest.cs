namespace CardGames.Poker.Api.Contracts;

public sealed record ModerateLeagueJoinRequestRequest
{
	public string? Reason { get; init; }
}
