using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public static class ChooseCardEndpoint
{
	public static RouteGroupBuilder MapChooseCard(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/choose-card",
				async (Guid gameId, ChooseCardRequest request, IMediator mediator, IGamePlayerResolver players,
					CancellationToken cancellationToken) =>
				{
					var seated = await players.ResolveSeatedPlayerAsync(gameId, cancellationToken);
					if (seated is null) return SeatedActor.NotSeated();

					var command = new ChooseCardCommand(gameId, request.Choice, seated.SeatPosition);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						Results.Ok,
						error => error.Code switch
						{
							ChooseCardErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ChooseCardErrorCode.NotInTollboothPhase => Results.Conflict(new { error.Message }),
							ChooseCardErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
							ChooseCardErrorCode.NoEligiblePlayers => Results.Conflict(new { error.Message }),
							ChooseCardErrorCode.AlreadyChosen => Results.Conflict(new { error.Message }),
							ChooseCardErrorCode.CannotAfford => Results.UnprocessableEntity(new { error.Message }),
							ChooseCardErrorCode.InvalidChoice => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("TollboothChooseCard")
			.WithSummary("Choose Tollbooth Card")
			.WithDescription(
				"Select a Tollbooth card: furthest display card (free), nearest display card (1× ante), or top deck card (2× ante).")
			.Produces<ChooseCardSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}