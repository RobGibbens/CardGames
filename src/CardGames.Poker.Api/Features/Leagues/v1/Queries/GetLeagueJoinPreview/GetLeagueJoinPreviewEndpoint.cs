using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueJoinPreview;

public static class GetLeagueJoinPreviewEndpoint
{
	public static RouteGroupBuilder MapGetLeagueJoinPreview(this RouteGroupBuilder group)
	{
		group.MapGet("join-preview",
				async (string token, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueJoinPreviewQuery(token), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueJoinPreviewErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueJoinPreviewErrorCode.InvalidInvite => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueJoinPreview")
			.WithSummary("Get league join preview")
			.WithDescription("Returns trust preview details for a league invite code.")
			.Produces<LeagueJoinPreviewDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}