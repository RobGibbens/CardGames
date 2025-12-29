using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentBettingRound;

/// <summary>
/// Endpoint for retrieving the current betting round for a specific game.
/// </summary>
public static class GetCurrentBettingRoundEndpoint
{
	public static RouteGroupBuilder MapGetCurrentBettingRound(this RouteGroupBuilder group)
	{
		group.MapGet("{gameId:guid}/betting-round",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetCurrentBettingRoundQuery(gameId), cancellationToken);
					return response is null
						? Results.NotFound()
						: Results.Ok(response);
				})
			.WithName($"FiveCardDraw{nameof(MapGetCurrentBettingRound).TrimPrefix("Map")}")
			.WithSummary(nameof(MapGetCurrentBettingRound).TrimPrefix("Map"))
			.WithDescription("Retrieve the current betting round for a specific game.")
			.MapToApiVersion(1.0)
			.Produces<GetCurrentBettingRoundResponse>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
