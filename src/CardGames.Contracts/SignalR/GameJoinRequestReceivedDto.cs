namespace CardGames.Contracts.SignalR;

public sealed record GameJoinRequestReceivedDto
{
	public required Guid GameId { get; init; }
	public required Guid JoinRequestId { get; init; }
	public required string GameName { get; init; }
	public required string HostName { get; init; }
	public required string PlayerName { get; init; }
	public string? PlayerAvatarUrl { get; init; }
	public string? PlayerFirstName { get; init; }
	public required int RequestedBuyIn { get; init; }
	public int? MaxBuyIn { get; init; }
	public required DateTimeOffset RequestedAtUtc { get; init; }
	public required DateTimeOffset ExpiresAtUtc { get; init; }
}