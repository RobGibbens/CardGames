using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public static class ProcessBuyCardEndpoint
{
	public static RouteGroupBuilder MapProcessBuyCard(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/buy-card",
				async (Guid gameId, ProcessBuyCardRequest request, IMediator mediator, IGamePlayerResolver players,
					CancellationToken cancellationToken) =>
				{
					var seated = await players.ResolveSeatedPlayerAsync(gameId, cancellationToken);
					if (seated is null) return SeatedActor.NotSeated();

					var command = new ProcessBuyCardCommand(gameId, seated.PlayerId, request.Accept);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ProcessBuyCardErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ProcessBuyCardErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							ProcessBuyCardErrorCode.NoPendingOffer => Results.Conflict(new { error.Message }),
							ProcessBuyCardErrorCode.InsufficientChips => Results.UnprocessableEntity(new
								{ error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"Baseball{nameof(MapProcessBuyCard).TrimPrefix("Map")}")
			.WithSummary("Process Buy Card")
			.WithDescription("Processes a buy-card decision for Baseball poker when a 4 is dealt face-up.")
			.Produces<ProcessBuyCardSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}

public record ProcessBuyCardRequest
{
	public bool Accept { get; init; }
}