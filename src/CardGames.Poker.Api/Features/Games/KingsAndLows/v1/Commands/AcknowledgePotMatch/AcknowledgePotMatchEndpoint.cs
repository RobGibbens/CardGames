using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;

public static class AcknowledgePotMatchEndpoint
{
	public static RouteGroupBuilder MapAcknowledgePotMatch(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/acknowledge-pot-match",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new AcknowledgePotMatchCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Acknowledge pot match error",
							detail: error.Message,
							statusCode: error.Code == AcknowledgePotMatchErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"KingsAndLows{nameof(MapAcknowledgePotMatch).TrimPrefix("Map")}")
			.WithSummary(nameof(MapAcknowledgePotMatch).TrimPrefix("Map"))
			.WithDescription("Process pot matching in Kings and Lows.")
			.Produces<AcknowledgePotMatchSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
