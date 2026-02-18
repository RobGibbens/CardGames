namespace CardGames.Poker.Api.Contracts;

public sealed record IngestLeagueSeasonEventResultsRequest
{
	public required IReadOnlyList<LeagueSeasonEventResultEntryRequest> Results { get; init; }
}
