using System.Text.Json.Serialization;

namespace CardGames.Poker.Api.Contracts;

public sealed record ResolveJoinRequestRequest(
	[property: JsonPropertyName("approved")] bool Approved,
	[property: JsonPropertyName("approvedBuyIn")] int? ApprovedBuyIn = null,
	[property: JsonPropertyName("denialReason")] string? DenialReason = null);