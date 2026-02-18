using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueInvites;

public static class GetLeagueInvitesEndpoint
{
	public static RouteGroupBuilder MapGetLeagueInvites(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/invites",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueInvitesQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueInvitesErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueInvitesErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueInvites")
			.WithSummary("Get league invites")
			.WithDescription("Returns league invites for active members.")
			.Produces<IReadOnlyList<LeagueInviteSummaryDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}