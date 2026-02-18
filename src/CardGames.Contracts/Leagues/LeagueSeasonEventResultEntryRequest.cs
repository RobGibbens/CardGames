namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueSeasonEventResultEntryRequest
{
	public required string MemberUserId { get; init; }

	public int Placement { get; init; }

	public int Points { get; init; }

	public int ChipsDelta { get; init; }
}
