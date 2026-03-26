using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public sealed record ResolveJoinRequestResponse
{
	[JsonPropertyName("gameId")]
	public required Guid GameId { get; init; }

	[JsonPropertyName("joinRequestId")]
	public required Guid JoinRequestId { get; init; }

	[JsonPropertyName("status")]
	public required string Status { get; init; }

	[JsonPropertyName("approvedBuyIn")]
	public int? ApprovedBuyIn { get; init; }

	[JsonPropertyName("seatIndex")]
	public int? SeatIndex { get; init; }
}