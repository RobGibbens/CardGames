namespace CardGames.Poker.Api.Contracts;

public sealed record LaunchLeagueEventSessionResponse
{
	public required Guid LeagueId { get; init; }

	public required Guid EventId { get; init; }

	public required Guid GameId { get; init; }

	public required string GameCode { get; init; }

	public required string TablePath { get; init; }

	public DateTimeOffset LaunchedAtUtc { get; init; }
}
