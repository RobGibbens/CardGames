using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

public static class PlaceBetEndpoint
{
	public static RouteGroupBuilder MapPlaceBet(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/place-bet",
				async (Guid gameId, PlaceBetRequest request, IMediator mediator, IGamePlayerResolver players,
					CancellationToken cancellationToken) =>
				{
					var seated = await players.ResolveSeatedPlayerAsync(gameId, cancellationToken);
					if (seated is null) return SeatedActor.NotSeated();

					var command = new PlaceBetCommand(gameId, seated.PlayerId, request.Amount);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						Results.Ok,
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
			.Produces<PlaceBetSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
///     Request body for placing a bet in In-Between.
/// </summary>
public record PlaceBetRequest(int Amount);