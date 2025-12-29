using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

/// <summary>
/// Endpoint for retrieving a specific game by its identifier.
/// </summary>
public static class GetGameEndpoint
{
	public static RouteGroupBuilder MapGetGame(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetGameQuery(gameId), cancellationToken);
					return response is null
						? Results.NotFound()
						: Results.Ok(response);
				})
			.WithName($"FiveCardDraw{nameof(MapGetGame).TrimPrefix("Map")}")
			.WithSummary(nameof(MapGetGame).TrimPrefix("Map"))
			.WithDescription("Retrieve a specific game by its identifier.")
			.MapToApiVersion(1.0)
			.Produces<GetGameResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
