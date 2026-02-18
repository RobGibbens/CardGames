using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;

public static class CreateLeagueInviteEndpoint
{
	public static RouteGroupBuilder MapCreateLeagueInvite(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/invites",
				async (Guid leagueId, CreateLeagueInviteRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CreateLeagueInviteCommand(leagueId, request), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							CreateLeagueInviteErrorCode.Unauthorized => Results.Unauthorized(),
							CreateLeagueInviteErrorCode.Forbidden => Results.Forbid(),
							CreateLeagueInviteErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
							CreateLeagueInviteErrorCode.InvalidExpiry => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CreateLeagueInvite")
			.WithSummary("Create league invite")
			.WithDescription("Creates a shareable invite URL for league join.")
			.Produces<CreateLeagueInviteResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}