namespace CardGames.Poker.Api.Contracts;

public sealed record LeagueOneOffEventsPageDto
{
	public required IReadOnlyList<LeagueOneOffEventDto> Entries { get; init; }

	public required bool HasMore { get; init; }

	public required int TotalCount { get; init; }

	public required int PageNumber { get; init; }

	public required int PageSize { get; init; }

	public required int TotalPages { get; init; }
}