namespace CardGames.Poker.Api.Contracts;

public sealed record UpdateGamePreferencesRequest
{
	public required int DefaultSmallBlind { get; init; }

	public required int DefaultBigBlind { get; init; }

	public required int DefaultAnte { get; init; }

	public required int DefaultMinimumBet { get; init; }
}
