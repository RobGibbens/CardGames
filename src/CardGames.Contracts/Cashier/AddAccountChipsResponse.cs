namespace CardGames.Poker.Api.Contracts;

public sealed record AddAccountChipsResponse
{
	public required int NewBalance { get; init; }

	public required int AppliedAmount { get; init; }

	public required Guid TransactionId { get; init; }

	public required string Message { get; init; }
}
