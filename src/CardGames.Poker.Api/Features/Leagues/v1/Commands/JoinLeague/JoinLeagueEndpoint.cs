using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;

public static class JoinLeagueEndpoint
{
	public static RouteGroupBuilder MapJoinLeague(this RouteGroupBuilder group)
	{
		Task<IResult> Handler(JoinLeagueRequest request, IMediator mediator, CancellationToken cancellationToken) =>
			HandleJoinAsync(request, mediator, cancellationToken);

		group.MapPost("join",
				Handler)
			.WithName("JoinLeague")
			.WithSummary("Join league")
			.WithDescription("Joins a league with a valid invite token.")
			.Produces<JoinLeagueResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		group.MapPost("join-by-invite",
				Handler)
			.WithName("JoinLeagueByInvite")
			.WithSummary("Join league by invite")
			.WithDescription("Compatibility alias for joining a league with a valid invite token.")
			.Produces<JoinLeagueResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}

	private static async Task<IResult> HandleJoinAsync(JoinLeagueRequest request, IMediator mediator, CancellationToken cancellationToken)
	{
		var result = await mediator.Send(new JoinLeagueCommand(request), cancellationToken);

		return result.Match(
			success => Results.Ok(success),
			error => error.Code switch
			{
				JoinLeagueErrorCode.Unauthorized => Results.Unauthorized(),
				JoinLeagueErrorCode.InvalidInvite => Results.BadRequest(new { error.Message }),
				_ => Results.Problem(error.Message)
			});
	}
}