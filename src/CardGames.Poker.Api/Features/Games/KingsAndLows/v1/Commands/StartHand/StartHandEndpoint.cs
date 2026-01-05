using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

public static class StartHandEndpoint
{
	public static RouteGroupBuilder MapStartHand(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/start-hand",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new StartHandCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Start hand error",
							detail: error.Message,
							statusCode: error.Code == StartHandErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"KingsAndLows{nameof(MapStartHand).TrimPrefix("Map")}")
			.WithSummary(nameof(MapStartHand).TrimPrefix("Map"))
			.WithDescription("Start a new hand in a Kings and Lows game.")
			.Produces<StartHandSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
