namespace CardGames.Poker.Api.Contracts;

public sealed record LaunchLeagueEventSessionRequest
{
	public required string GameCode { get; init; }

	public string? GameName { get; init; }

	public int Ante { get; init; } = 10;

	public int MinBet { get; init; } = 20;

	public int HostStartingChips { get; init; } = 100;
}
