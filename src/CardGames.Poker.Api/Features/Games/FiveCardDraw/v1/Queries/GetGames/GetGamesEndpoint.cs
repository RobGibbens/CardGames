using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGames;

public static class GetGamesEndpoint
{
	public static RouteGroupBuilder MapGetGames(this RouteGroupBuilder group)
	{
		group.MapGet("",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetGamesQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName($"FiveCardDraw{nameof(MapGetGames).TrimPrefix("Map")}")
			.WithSummary(nameof(MapGetGames).TrimPrefix("Map"))
			.WithDescription("Retrieve a list of games.")
			.MapToApiVersion(1.0)
			.Produces<List<GetGamesResponse>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}