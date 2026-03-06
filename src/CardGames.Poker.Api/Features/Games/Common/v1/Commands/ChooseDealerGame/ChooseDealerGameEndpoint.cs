using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public static class ChooseDealerGameEndpoint
{
	public static RouteGroupBuilder MapChooseDealerGame(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/choose-game",
				async (Guid gameId, ChooseDealerGameRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ChooseDealerGameCommand(gameId, request.GameTypeCode, request.Ante, request.MinBet, request.SmallBlind, request.BigBlind);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Problem(
							title: "Choose dealer game failed",
							detail: error.Reason,
							statusCode: StatusCodes.Status400BadRequest)
					);
				})
			.WithName("ChooseDealerGame")
			.WithSummary("Choose the game type for the current hand (Dealer's Choice)")
			.WithDescription("Allows the current Dealer's Choice dealer to select the game type, ante, and minimum bet for the upcoming hand.")
			.Produces(StatusCodes.Status200OK, typeof(ChooseDealerGameSuccessful))
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}
