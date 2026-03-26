using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public static class ResolveJoinRequestEndpoint
{
	public static RouteGroupBuilder MapResolveJoinRequest(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/join-requests/{joinRequestId:guid}/resolve",
				async (Guid gameId, Guid joinRequestId, ResolveJoinRequestRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(
						new ResolveJoinRequestCommand(gameId, joinRequestId, request.Approved, request.ApprovedBuyIn, request.DenialReason),
						cancellationToken);

					return result.Match<IResult>(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ResolveJoinRequestErrorCode.NotFound => Results.NotFound(new { error.Message }),
							ResolveJoinRequestErrorCode.NotHost => Results.Forbid(),
							ResolveJoinRequestErrorCode.InvalidApprovedBuyIn => Results.BadRequest(new { error.Message }),
							ResolveJoinRequestErrorCode.AlreadyResolved => Results.Conflict(new { error.Message }),
							ResolveJoinRequestErrorCode.Expired => Results.Conflict(new { error.Message }),
							ResolveJoinRequestErrorCode.SeatUnavailable => Results.Conflict(new { error.Message }),
							ResolveJoinRequestErrorCode.InsufficientAccountChips => Results.Conflict(new { error.Message }),
							ResolveJoinRequestErrorCode.GameEnded => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName($"{nameof(MapResolveJoinRequest).TrimPrefix("Map")}")
			.WithSummary("Resolve Join Request")
			.Produces<ResolveJoinRequestSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}