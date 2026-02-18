using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DemoteLeagueAdminToMember;

public static class DemoteLeagueAdminToMemberEndpoint
{
	public static RouteGroupBuilder MapDemoteLeagueAdminToMember(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/members/{memberUserId}/demote-admin",
				async (Guid leagueId, string memberUserId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new DemoteLeagueAdminToMemberCommand(leagueId, memberUserId), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							DemoteLeagueAdminToMemberErrorCode.Unauthorized => Results.Unauthorized(),
							DemoteLeagueAdminToMemberErrorCode.Forbidden => Results.Forbid(),
							DemoteLeagueAdminToMemberErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							DemoteLeagueAdminToMemberErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							DemoteLeagueAdminToMemberErrorCode.Conflict => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("DemoteLeagueAdminToMember")
			.WithSummary("Demote league admin to member")
			.WithDescription("Demotes an active league admin to member while preserving league governance invariants.")
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
