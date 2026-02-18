using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembers;

public static class GetLeagueMembersEndpoint
{
	public static RouteGroupBuilder MapGetLeagueMembers(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/members",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueMembersQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueMembersErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueMembersErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueMembers")
			.WithSummary("Get league members")
			.WithDescription("Returns members for an active member of the league.")
			.Produces<IReadOnlyList<LeagueMemberDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}