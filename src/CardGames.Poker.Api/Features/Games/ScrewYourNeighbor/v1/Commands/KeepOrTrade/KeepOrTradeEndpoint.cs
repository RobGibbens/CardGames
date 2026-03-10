using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

public static class KeepOrTradeEndpoint
{
	public static RouteGroupBuilder MapKeepOrTrade(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/keep-or-trade",
				async (Guid gameId, KeepOrTradeRequest request, IMediator mediator,
					CancellationToken cancellationToken) =>
				{
					var command = new KeepOrTradeCommand(gameId, request.PlayerId, request.Decision);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Keep or trade error",
							detail: error.Message,
							statusCode: error.Code == KeepOrTradeErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"ScrewYourNeighbor{nameof(MapKeepOrTrade).TrimPrefix("Map")}")
			.WithSummary(nameof(MapKeepOrTrade).TrimPrefix("Map"))
			.WithDescription("Record a player's keep or trade decision in Screw Your Neighbor.")
			.Produces<KeepOrTradeSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
/// Request body for keep or trade decision.
/// </summary>
public record KeepOrTradeRequest(Guid PlayerId, string Decision);
