using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.CollectAntes;

public static class CollectAntesEndpoint
{
	public static RouteGroupBuilder MapCollectAntes(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands/antes",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CollectAntesCommand(gameId), cancellationToken);
					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							CollectAntesErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							CollectAntesErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName($"Tollbooth{nameof(MapCollectAntes).TrimPrefix("Map")}")
			.WithSummary("Collect Antes")
			.WithDescription("Collects antes from active Tollbooth players and advances the game to Third Street.")
			.Produces<CollectAntesSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
