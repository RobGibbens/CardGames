using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LeaveLeague;

public static class LeaveLeagueEndpoint
{
	public static RouteGroupBuilder MapLeaveLeague(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/leave",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new LeaveLeagueCommand(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							LeaveLeagueErrorCode.Unauthorized => Results.Unauthorized(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("LeaveLeague")
			.WithSummary("Leave league")
			.WithDescription("Leaves the league for the current user. If not active member, returns no-op success.")
			.Produces<LeaveLeagueResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.RequireAuthorization();

		return group;
	}
}