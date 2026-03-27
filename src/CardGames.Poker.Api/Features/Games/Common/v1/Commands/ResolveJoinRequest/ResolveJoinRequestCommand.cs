using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public sealed record ResolveJoinRequestCommand(
	Guid GameId,
	Guid JoinRequestId,
	bool Approved,
	int? ApprovedBuyIn,
	string? DenialReason) : IRequest<OneOf<ResolveJoinRequestSuccessful, ResolveJoinRequestError>>, IGameStateChangingCommand;