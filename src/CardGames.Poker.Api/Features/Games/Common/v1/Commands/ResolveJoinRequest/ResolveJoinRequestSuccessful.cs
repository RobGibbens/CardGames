namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public sealed record ResolveJoinRequestSuccessful(Guid GameId, Guid JoinRequestId, string Status, int? ApprovedBuyIn, int? SeatIndex);