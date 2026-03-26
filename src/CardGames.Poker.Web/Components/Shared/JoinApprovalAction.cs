namespace CardGames.Poker.Web.Components.Shared;

public sealed record JoinApprovalAction(Guid GameId, Guid JoinRequestId, bool Approved, int? ApprovedBuyIn);