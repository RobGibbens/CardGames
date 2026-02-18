namespace CardGames.Poker.Api.Contracts;

public sealed record JoinLeagueRequest
{
	public required string Token { get; init; }
}