using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

public static class DropOrStayEndpoint
{
	public static RouteGroupBuilder MapDropOrStay(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/drop-or-stay",
				async (Guid gameId, DropOrStayRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new DropOrStayCommand(gameId, request.PlayerId, request.Decision);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Drop or stay error",
							detail: error.Message,
							statusCode: error.Code == DropOrStayErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"KingsAndLows{nameof(MapDropOrStay).TrimPrefix("Map")}")
			.WithSummary(nameof(MapDropOrStay).TrimPrefix("Map"))
			.WithDescription("Record a player's drop or stay decision in Kings and Lows.")
			.Produces<DropOrStaySuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
/// Request body for drop or stay decision.
/// </summary>
public record DropOrStayRequest(Guid PlayerId, string Decision);
