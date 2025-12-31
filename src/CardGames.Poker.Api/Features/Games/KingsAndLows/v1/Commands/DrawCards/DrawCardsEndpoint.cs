using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

public static class DrawCardsEndpoint
{
	public static RouteGroupBuilder MapDrawCards(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/draw",
				async (Guid gameId, DrawCardsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new DrawCardsCommand(gameId, request.PlayerId, request.DiscardIndices);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Draw cards error",
							detail: error.Message,
							statusCode: error.Code == DrawCardsErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"KingsAndLows{nameof(MapDrawCards).TrimPrefix("Map")}")
			.WithSummary(nameof(MapDrawCards).TrimPrefix("Map"))
			.WithDescription("Process a player's draw action in Kings and Lows.")
			.Produces<DrawCardsSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
