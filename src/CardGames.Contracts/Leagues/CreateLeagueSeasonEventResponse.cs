namespace CardGames.Poker.Api.Contracts;

public sealed record CreateLeagueSeasonEventResponse
{
	public required Guid EventId { get; init; }

	public required Guid LeagueId { get; init; }

	public required Guid SeasonId { get; init; }

	public required string Name { get; init; }

	public int? SequenceNumber { get; init; }

	public DateTimeOffset? ScheduledAtUtc { get; init; }

	public LeagueSeasonEventStatus Status { get; init; }

	public string? Notes { get; init; }

	public required string CreatedByUserId { get; init; }

	public DateTimeOffset CreatedAtUtc { get; init; }

	public int? TournamentBuyIn { get; init; }
}