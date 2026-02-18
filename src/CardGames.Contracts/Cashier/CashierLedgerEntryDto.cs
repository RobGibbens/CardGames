namespace CardGames.Poker.Api.Contracts;

public sealed record CashierLedgerEntryDto
{
	public required Guid Id { get; init; }

	public required DateTimeOffset OccurredAtUtc { get; init; }

	public required string Type { get; init; }

	public required int AmountDelta { get; init; }

	public required int BalanceAfter { get; init; }

	public string? ReferenceType { get; init; }

	public Guid? ReferenceId { get; init; }

	public string? Reason { get; init; }
}
