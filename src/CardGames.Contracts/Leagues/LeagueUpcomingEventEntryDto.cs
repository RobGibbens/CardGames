namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueUpcomingEventEntryDto
{
	public required DateTimeOffset SortAt { get; init; }

	public LeagueSeasonEventDto? SeasonEvent { get; init; }

	public LeagueOneOffEventDto? OneOffEvent { get; init; }
}