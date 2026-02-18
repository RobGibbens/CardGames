namespace CardGames.Poker.Api.Contracts;

public sealed record AddAccountChipsRequest
{
	public required int Amount { get; init; }

	public string? Reason { get; init; }
}
