namespace CardGames.Poker.Api.Contracts;

public sealed record CorrectLeagueSeasonEventResultsRequest
{
	public required string Reason { get; init; }

	public required IReadOnlyList<LeagueSeasonEventResultEntryRequest> Results { get; init; }
}
