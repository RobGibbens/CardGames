using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.StartHand;

public static class StartHandEndpoint
{
	public static RouteGroupBuilder MapStartHand(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new StartHandCommand(gameId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							StartHandErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							StartHandErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							StartHandErrorCode.NotEnoughPlayers => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName($"Tollbooth{nameof(MapStartHand).TrimPrefix("Map")}")
			.WithSummary("Start Hand")
			.WithDescription("Starts a new hand of Tollbooth and transitions the game to the CollectingAntes phase.")
			.Produces<StartHandSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
