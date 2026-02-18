using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RevokeLeagueInvite;

public static class RevokeLeagueInviteEndpoint
{
	public static RouteGroupBuilder MapRevokeLeagueInvite(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/invites/{inviteId:guid}/revoke",
				async (Guid leagueId, Guid inviteId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new RevokeLeagueInviteCommand(leagueId, inviteId), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							RevokeLeagueInviteErrorCode.Unauthorized => Results.Unauthorized(),
							RevokeLeagueInviteErrorCode.Forbidden => Results.Forbid(),
							RevokeLeagueInviteErrorCode.InviteNotFound => Results.NotFound(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("RevokeLeagueInvite")
			.WithSummary("Revoke league invite")
			.WithDescription("Revokes a league invite so it can no longer be used.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}