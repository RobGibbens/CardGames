namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueSeasonEventRequest
{
	public DateTimeOffset ScheduledAtUtc { get; init; }

	public string? Notes { get; init; }

	public string? GameTypeCode { get; init; }

	public int? Ante { get; init; }

	public int? MinBet { get; init; }

	public int? SmallBlind { get; init; }

	public int? BigBlind { get; init; }

	public int? TournamentBuyIn { get; init; }
}