using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;

public static class PromoteLeagueMemberToAdminEndpoint
{
	public static RouteGroupBuilder MapPromoteLeagueMemberToAdmin(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/members/{memberUserId}/promote-admin",
				async (Guid leagueId, string memberUserId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new PromoteLeagueMemberToAdminCommand(leagueId, memberUserId), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							PromoteLeagueMemberToAdminErrorCode.Unauthorized => Results.Unauthorized(),
							PromoteLeagueMemberToAdminErrorCode.Forbidden => Results.Forbid(),
							PromoteLeagueMemberToAdminErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("PromoteLeagueMemberToAdmin")
			.WithSummary("Promote league member to admin")
			.WithDescription("Promotes an active league member to admin role.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}