using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

public static class PlaceBetEndpoint
{
	public static RouteGroupBuilder MapPlaceBet(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/place-bet",
				async (Guid gameId, PlaceBetRequest request, IMediator mediator,
					CancellationToken cancellationToken) =>
				{
					var command = new PlaceBetCommand(gameId, request.PlayerId, request.Amount);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Place bet error",
							detail: error.Message,
							statusCode: error.Code == PlaceBetErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"InBetween{nameof(MapPlaceBet).TrimPrefix("Map")}")
			.WithSummary(nameof(MapPlaceBet).TrimPrefix("Map"))
			.WithDescription("Place a bet (or pass with amount 0) during an In-Between turn.")
			.Produces<PlaceBetSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
/// Request body for placing a bet in In-Between.
/// </summary>
public record PlaceBetRequest(Guid PlayerId, int Amount);
