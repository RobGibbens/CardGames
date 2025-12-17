using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CreateGame;

public static class CreateGameEndpoint
{
	public static RouteGroupBuilder MapCreateGame(this RouteGroupBuilder group)
	{
		group.MapPost("",
				async (CreateGameCommand command, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Created($"/api/games/five-card-draw/{success.GameId}", success.GameId)
					);
				})
			.WithName(nameof(MapCreateGame).TrimPrefix("Map"))
			.WithSummary(nameof(MapCreateGame).TrimPrefix("Map"))
			.WithDescription("Create a new game.")
			.Produces(StatusCodes.Status201Created, typeof(Guid))
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}