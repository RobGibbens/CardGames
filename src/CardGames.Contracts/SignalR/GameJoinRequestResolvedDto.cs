namespace CardGames.Contracts.SignalR;

public sealed record GameJoinRequestResolvedDto
{
	public required Guid GameId { get; init; }
	public required Guid JoinRequestId { get; init; }
	public required string Status { get; init; }
	public required string HostName { get; init; }
	public string? PlayerName { get; init; }
	public int? ApprovedBuyIn { get; init; }
	public string? Reason { get; init; }
	public required DateTimeOffset ResolvedAtUtc { get; init; }
}