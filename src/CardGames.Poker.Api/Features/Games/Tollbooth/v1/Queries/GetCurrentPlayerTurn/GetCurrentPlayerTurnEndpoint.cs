using CardGames.Poker.Api.Extensions;
using MediatR;
using SharedGetCurrentPlayerTurnResponse = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnResponse;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Queries.GetCurrentPlayerTurn;

public static class GetCurrentPlayerTurnEndpoint
{
	public static RouteGroupBuilder MapGetCurrentPlayerTurn(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}/current-turn",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetCurrentPlayerTurnQuery(gameId), cancellationToken);
					return response is null
						? Results.NotFound()
						: Results.Ok(response);
				})
			.WithName($"Tollbooth{nameof(MapGetCurrentPlayerTurn).TrimPrefix("Map")}")
			.WithSummary(nameof(MapGetCurrentPlayerTurn).TrimPrefix("Map"))
			.WithDescription("Retrieve the current player's turn state for a specific Tollbooth game, including available actions.")
			.MapToApiVersion(1.0)
			.Produces<SharedGetCurrentPlayerTurnResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
