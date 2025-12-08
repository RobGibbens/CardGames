using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;

/// <summary>
/// Endpoint for collecting antes from all players in a Five Card Draw game.
/// </summary>
public static class CollectAntesEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for collecting antes.
	/// </summary>
	public static RouteGroupBuilder MapCollectAntes(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands/antes",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new CollectAntesCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							CollectAntesErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							CollectAntesErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName(nameof(MapCollectAntes).TrimPrefix("Map"))
			.WithSummary("Collect Antes")
			.WithDescription("Collects the mandatory ante bet from all players to seed the pot before dealing. " +
			                 "Each player contributes the ante amount (or their remaining chips if short-stacked). " +
			                 "After collection, the game transitions to the Dealing phase. " +
			                 "Players with fewer chips than the ante will contribute their entire stack (going all-in on the ante).")
			.Produces<CollectAntesSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
