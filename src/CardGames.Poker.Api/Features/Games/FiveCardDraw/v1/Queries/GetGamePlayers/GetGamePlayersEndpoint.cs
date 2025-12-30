using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;

/// <summary>
/// Endpoint for retrieving all players in a specific game.
/// </summary>
public static class GetGamePlayersEndpoint
{
	public static RouteGroupBuilder MapGetGamePlayers(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}/players",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetGamePlayersQuery(gameId), cancellationToken);
					return Results.Ok(response);
				})
			.WithName($"FiveCardDraw{nameof(MapGetGamePlayers).TrimPrefix("Map")}")
			.WithSummary(nameof(MapGetGamePlayers).TrimPrefix("Map"))
			.WithDescription("Retrieve all players in a specific game.")
			.MapToApiVersion(1.0)
			.Produces<List<GetGamePlayersResponse>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
