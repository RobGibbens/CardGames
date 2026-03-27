namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public sealed record ResolveJoinRequestError(ResolveJoinRequestErrorCode Code, string Message);

public enum ResolveJoinRequestErrorCode
{
	NotFound,
	NotHost,
	AlreadyResolved,
	Expired,
	InvalidApprovedBuyIn,
	SeatUnavailable,
	InsufficientAccountChips,
	GameEnded
}