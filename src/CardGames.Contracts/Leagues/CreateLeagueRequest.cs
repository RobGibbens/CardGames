namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueRequest
{
	public required string Name { get; init; }

	public string? Description { get; init; }
}