using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;

/// <summary>
/// Endpoint for dealing hands to all players in a Five Card Draw game.
/// </summary>
public static class DealHandsEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for dealing hands.
	/// </summary>
	public static RouteGroupBuilder MapDealHands(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands/deal",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new DealHandsCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							DealHandsErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							DealHandsErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							DealHandsErrorCode.InsufficientCards => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"FiveCardDraw{nameof(MapDealHands).TrimPrefix("Map")}")
			.WithSummary("Deal Hands")
			.WithDescription("Deals five cards to each active player from the shuffled deck and initiates the first betting round. " +
			                 "After dealing, the game transitions to the FirstBettingRound phase. " +
			                 "This method deals 5 cards to each player who has not folded, " +
			                 "resets all players' current bet amounts for the new betting round, " +
			                 "and automatically starts the first betting round.")
			.Produces<DealHandsSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}
