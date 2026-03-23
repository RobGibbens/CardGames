using CardGames.Poker.Api.Extensions;
using MediatR;
using SharedDealHandsErrorCode = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsErrorCode;
using SharedDealHandsSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.DealHands;

public static class DealHandsEndpoint
{
	public static RouteGroupBuilder MapDealHands(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands/deal",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new DealHandsCommand(gameId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							SharedDealHandsErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							SharedDealHandsErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							SharedDealHandsErrorCode.InsufficientCards => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"PairPressure{nameof(MapDealHands).TrimPrefix("Map")}")
			.WithSummary("Deal Hands")
			.WithDescription("Deals the current Pair Pressure street and advances play to the next betting actor.")
			.Produces<SharedDealHandsSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}