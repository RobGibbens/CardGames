using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

public static class GetActiveGamesEndpoint
{
	public static RouteGroupBuilder MapGetActiveGames(this RouteGroupBuilder group)
	{
		group.MapGet("",
				async (string? variant, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetActiveGamesQuery(variant), cancellationToken);
					return Results.Ok(response);
				})
			.WithName(nameof(MapGetActiveGames).TrimPrefix("Map"))
			.WithSummary(nameof(MapGetActiveGames).TrimPrefix("Map"))
			.WithDescription("Retrieve a list of all non-complete games.")
			.MapToApiVersion(1.0)
			.Produces<List<GetActiveGamesResponse>>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
