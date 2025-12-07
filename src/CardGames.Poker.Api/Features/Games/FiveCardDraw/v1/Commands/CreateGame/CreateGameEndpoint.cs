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
						success => Results.Created($"/api/games/{success.GameId}", new { success.GameId })
					);
				})
			.WithName(nameof(MapCreateGame).TrimPrefix("Map"))
			.WithSummary(nameof(MapCreateGame).TrimPrefix("Map"))
			.WithDescription("Add a new category.")
			.Produces(StatusCodes.Status201Created)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}