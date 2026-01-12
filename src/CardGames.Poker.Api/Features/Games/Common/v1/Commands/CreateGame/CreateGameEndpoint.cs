using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;

public static class CreateGameEndpoint
{
	public static RouteGroupBuilder MapCreateGame(this RouteGroupBuilder group)
	{
		group.MapPost("",
				async (CreateGameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Created($"/api/games/{success.GameId}", success.GameId),
						conflict => Results.Problem(
							title: "Create game conflict",
							detail: conflict.Reason,
							statusCode: StatusCodes.Status409Conflict)
					);
				})
			.WithName($"{nameof(MapCreateGame).TrimPrefix("Map")}")
			.WithSummary(nameof(MapCreateGame).TrimPrefix("Map"))
			.WithDescription("Create a new game.")
			.Produces(StatusCodes.Status201Created, typeof(Guid))
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}