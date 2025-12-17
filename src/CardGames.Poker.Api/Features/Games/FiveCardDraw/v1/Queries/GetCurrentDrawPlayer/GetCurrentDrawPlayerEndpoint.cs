using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentDrawPlayer;

/// <summary>
/// Endpoint for retrieving the current player who must act during the draw phase.
/// </summary>
public static class GetCurrentDrawPlayerEndpoint
{
	public static RouteGroupBuilder MapGetCurrentDrawPlayer(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}/draw-player",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetCurrentDrawPlayerQuery(gameId), cancellationToken);
					return response is null
						? Results.NotFound()
						: Results.Ok(response);
				})
			.WithName(nameof(MapGetCurrentDrawPlayer).TrimPrefix("Map"))
			.WithSummary(nameof(MapGetCurrentDrawPlayer).TrimPrefix("Map"))
			.WithDescription("Retrieve the current player who must act during the draw phase. Returns 404 if not in draw phase or no eligible players remain.")
			.MapToApiVersion(1.0)
			.Produces<GetCurrentDrawPlayerResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
