namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public sealed record ResolveJoinRequestRequest(bool Approved, int? ApprovedBuyIn = null, string? DenialReason = null);