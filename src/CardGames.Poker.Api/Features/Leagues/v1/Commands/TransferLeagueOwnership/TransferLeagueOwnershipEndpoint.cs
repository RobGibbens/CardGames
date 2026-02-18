using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.TransferLeagueOwnership;

public static class TransferLeagueOwnershipEndpoint
{
	public static RouteGroupBuilder MapTransferLeagueOwnership(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/members/{memberUserId}/transfer-ownership",
				async (Guid leagueId, string memberUserId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new TransferLeagueOwnershipCommand(leagueId, memberUserId), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							TransferLeagueOwnershipErrorCode.Unauthorized => Results.Unauthorized(),
							TransferLeagueOwnershipErrorCode.Forbidden => Results.Forbid(),
							TransferLeagueOwnershipErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							TransferLeagueOwnershipErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("TransferLeagueOwnership")
			.WithSummary("Transfer league ownership")
			.WithDescription("Transfers manager ownership to another active league member.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}
