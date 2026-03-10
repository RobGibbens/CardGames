using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;

public static class UpdateGamePreferencesEndpoint
{
	public static RouteGroupBuilder MapUpdateGamePreferences(this RouteGroupBuilder group)
	{
		group.MapPut("game-preferences",
				async (UpdateGamePreferencesRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(
						new UpdateGamePreferencesCommand(
							request.DefaultSmallBlind,
							request.DefaultBigBlind,
							request.DefaultAnte,
							request.DefaultMinimumBet),
						cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							UpdateGamePreferencesErrorCode.Unauthorized => Results.Unauthorized(),
							UpdateGamePreferencesErrorCode.InvalidPreferences => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("UpdateGamePreferences")
			.WithSummary("Update game preferences")
			.WithDescription("Creates or updates default blind, ante, and minimum bet preferences for the authenticated player.")
			.Produces<GamePreferencesDto>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}
