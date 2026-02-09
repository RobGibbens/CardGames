using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public static class GetCurrentPlayerTurnEndpoint
{
	public static RouteGroupBuilder MapGetCurrentPlayerTurn(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}/current-turn",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var query = new GetCurrentPlayerTurnQuery(gameId);
					var response = await mediator.Send(query, cancellationToken);

					return response is null
						? Results.NotFound()
						: Results.Ok(response);
				})
			.WithName($"Baseball{nameof(MapGetCurrentPlayerTurn).TrimPrefix("Map")}")
			.WithSummary("Get Current Player Turn")
			.WithDescription("Gets the current player's turn state, including available actions and hand odds.")
			.Produces<GetCurrentPlayerTurnResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound);

		return group;
	}
}
