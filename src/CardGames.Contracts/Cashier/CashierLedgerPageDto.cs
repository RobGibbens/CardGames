namespace CardGames.Poker.Api.Contracts;

public sealed record CashierLedgerPageDto
{
	public required IReadOnlyList<CashierLedgerEntryDto> Entries { get; init; }

	public required bool HasMore { get; init; }

	public required int TotalCount { get; init; }
}
