namespace CardGames.Poker.Api.Data.Entities;

public class PlayerChipAccount : EntityWithRowVersion
{
	public Guid PlayerId { get; set; }

	public int Balance { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public Player Player { get; set; } = null!;
}

public enum PlayerChipLedgerEntryType
{
	Add = 1,
	BuyIn = 2,
	CashOut = 3,
	Adjustment = 4,
	HandSettlement = 5,
	BringIn = 6
}

public class PlayerChipLedgerEntry : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid PlayerId { get; set; }

	public PlayerChipLedgerEntryType Type { get; set; }

	public int AmountDelta { get; set; }

	public int BalanceAfter { get; set; }

	public DateTimeOffset OccurredAtUtc { get; set; }

	public string? ReferenceType { get; set; }

	public Guid? ReferenceId { get; set; }

	public string? Reason { get; set; }

	public string? ActorUserId { get; set; }

	/// <summary>
	/// The hand number associated with this entry (populated for HandSettlement entries).
	/// </summary>
	public int? HandNumber { get; set; }

	public Player Player { get; set; } = null!;
}
