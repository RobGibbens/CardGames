using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;

public static class GetAvailablePokerGamesEndpoint
{
	public static RouteGroupBuilder MapGetAvailablePokerGames(this RouteGroupBuilder group)
	{
		group.MapGet("",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetAvailablePokerGamesQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName(nameof(MapGetAvailablePokerGames).TrimPrefix("Map"))
			.WithSummary(nameof(MapGetAvailablePokerGames).TrimPrefix("Map"))
			.WithDescription("Retrieve a list of all available poker game types.")
			.MapToApiVersion(1.0)
			.Produces<List<GetAvailablePokerGamesResponse>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
