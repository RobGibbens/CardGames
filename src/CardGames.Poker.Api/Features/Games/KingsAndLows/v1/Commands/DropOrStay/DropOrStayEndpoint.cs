using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

public static class DropOrStayEndpoint
{
	public static RouteGroupBuilder MapDropOrStay(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/drop-or-stay",
				async (Guid gameId, DropOrStayRequest request, IMediator mediator, IGamePlayerResolver players,
					CancellationToken cancellationToken) =>
				{
					var seated = await players.ResolveSeatedPlayerAsync(gameId, cancellationToken);
					if (seated is null) return SeatedActor.NotSeated();

					var command = new DropOrStayCommand(gameId, seated.PlayerId, request.Decision);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						Results.Ok,
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
			.Produces<DropOrStaySuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
///     Request body for drop or stay decision.
/// </summary>
public record DropOrStayRequest(string Decision);