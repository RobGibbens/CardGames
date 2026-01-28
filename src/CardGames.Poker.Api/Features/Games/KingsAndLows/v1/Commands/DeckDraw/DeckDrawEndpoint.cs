using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

public static class DeckDrawEndpoint
{
	public static RouteGroupBuilder MapDeckDraw(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/deck-draw",
				async (Guid gameId, DeckDrawRequest request, IMediator mediator, IGameStateBroadcaster broadcaster, CancellationToken cancellationToken) =>
				{
					var command = new DeckDrawCommand(gameId, request.PlayerId, request.DiscardIndices);
					var result = await mediator.Send(command, cancellationToken);

					return await result.Match(
						async success =>
						{
							// Broadcast updated game state to all players after deck draw
							await broadcaster.BroadcastGameStateAsync(gameId, cancellationToken);
							return Results.Ok(success);
						},
						error => Task.FromResult(Results.Problem(
							title: "Deck draw error",
							detail: error.Message,
							statusCode: error.Code == DeckDrawErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest))
					);
				})
			.WithName($"KingsAndLows{nameof(MapDeckDraw).TrimPrefix("Map")}")
			.WithSummary(nameof(MapDeckDraw).TrimPrefix("Map"))
			.WithDescription("Process the deck's draw in player-vs-deck scenario.")
			.Produces<DeckDrawSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
