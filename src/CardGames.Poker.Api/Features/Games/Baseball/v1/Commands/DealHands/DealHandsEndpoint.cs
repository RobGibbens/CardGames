using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

public static class DealHandsEndpoint
{
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
			.WithName($"Baseball{nameof(MapDealHands).TrimPrefix("Map")}")
			.WithSummary("Deal Hands")
			.WithDescription("Deals the next street of cards for Baseball and sets up the betting round.")
			.Produces<DealHandsSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}
