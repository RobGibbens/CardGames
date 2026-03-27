namespace CardGames.Poker.Api.Data.Entities;

public sealed class GameJoinRequest : EntityWithRowVersion
{
	public Guid Id { get; set; } = Guid.CreateVersion7();
	public Guid GameId { get; set; }
	public Game Game { get; set; } = null!;
	public Guid PlayerId { get; set; }
	public Player Player { get; set; } = null!;
	public int RequestedBuyIn { get; set; }
	public int? ApprovedBuyIn { get; set; }
	public int SeatIndex { get; set; }
	public GameJoinRequestStatus Status { get; set; } = GameJoinRequestStatus.Pending;
	public DateTimeOffset RequestedAt { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public DateTimeOffset? ResolvedAt { get; set; }
	public string? ResolvedByUserId { get; set; }
	public string? ResolvedByName { get; set; }
	public string? ResolutionReason { get; set; }
}

public enum GameJoinRequestStatus
{
	Pending = 0,
	Approved = 1,
	Denied = 2,
	Expired = 3,
	Cancelled = 4
}