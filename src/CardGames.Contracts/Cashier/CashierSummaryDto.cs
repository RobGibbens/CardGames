namespace CardGames.Poker.Api.Contracts;

public sealed record CashierSummaryDto
{
	public required int CurrentBalance { get; init; }

	public int PendingBalanceChange { get; init; }

	public DateTimeOffset? LastTransactionAtUtc { get; init; }
}
