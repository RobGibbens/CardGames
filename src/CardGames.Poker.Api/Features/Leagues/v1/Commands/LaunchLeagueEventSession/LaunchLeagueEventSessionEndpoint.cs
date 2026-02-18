using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;

public static class LaunchLeagueEventSessionEndpoint
{
	public static RouteGroupBuilder MapLaunchLeagueEventSession(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/seasons/{seasonId:guid}/events/{eventId:guid}/launch",
				async (Guid leagueId, Guid seasonId, Guid eventId, LaunchLeagueEventSessionRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(
						new LaunchLeagueEventSessionCommand(leagueId, LeagueEventSourceType.Season, eventId, seasonId, request),
						cancellationToken);

					return ToHttpResult(result);
				})
			.WithName("LaunchLeagueSeasonEventSession")
			.WithSummary("Launch session from league season event")
			.WithDescription("Creates a playable game session from a season event and links the event to the created table.")
			.Produces<LaunchLeagueEventSessionResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.RequireAuthorization();

		group.MapPost("{leagueId:guid}/events/one-off/{eventId:guid}/launch",
				async (Guid leagueId, Guid eventId, LaunchLeagueEventSessionRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(
						new LaunchLeagueEventSessionCommand(leagueId, LeagueEventSourceType.OneOff, eventId, null, request),
						cancellationToken);

					return ToHttpResult(result);
				})
			.WithName("LaunchLeagueOneOffEventSession")
			.WithSummary("Launch session from league one-off event")
			.WithDescription("Creates a playable game session from a one-off event and links the event to the created table.")
			.Produces<LaunchLeagueEventSessionResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.RequireAuthorization();

		return group;
	}

	private static IResult ToHttpResult(OneOf.OneOf<LaunchLeagueEventSessionResponse, LaunchLeagueEventSessionError> result)
	{
		return result.Match<IResult>(
			success => Results.Ok(success),
			error => error.Code switch
			{
				LaunchLeagueEventSessionErrorCode.Unauthorized => Results.Unauthorized(),
				LaunchLeagueEventSessionErrorCode.Forbidden => Results.Forbid(),
				LaunchLeagueEventSessionErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
				LaunchLeagueEventSessionErrorCode.EventNotFound => Results.NotFound(new { error.Message }),
				LaunchLeagueEventSessionErrorCode.MismatchedLeagueOrSeason => Results.BadRequest(new { error.Message }),
				LaunchLeagueEventSessionErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
				LaunchLeagueEventSessionErrorCode.AlreadyLaunched => Results.Conflict(new { error.Message }),
				LaunchLeagueEventSessionErrorCode.CreateGameConflict => Results.Conflict(new { error.Message }),
				_ => Results.Problem(error.Message)
			});
	}
}
