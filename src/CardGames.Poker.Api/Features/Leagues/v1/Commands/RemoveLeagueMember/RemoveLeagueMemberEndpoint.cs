using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RemoveLeagueMember;

public static class RemoveLeagueMemberEndpoint
{
	public static RouteGroupBuilder MapRemoveLeagueMember(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/members/{memberUserId}/remove",
				async (Guid leagueId, string memberUserId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new RemoveLeagueMemberCommand(leagueId, memberUserId), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							RemoveLeagueMemberErrorCode.Unauthorized => Results.Unauthorized(),
							RemoveLeagueMemberErrorCode.Forbidden => Results.Forbid(),
							RemoveLeagueMemberErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							RemoveLeagueMemberErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							RemoveLeagueMemberErrorCode.Conflict => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("RemoveLeagueMember")
			.WithSummary("Remove league member")
			.WithDescription("Removes an active league member while preserving league governance invariants.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.RequireAuthorization();

		return group;
	}
}
