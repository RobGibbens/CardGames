using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public static class ProcessBuyCardEndpoint
{
	public static RouteGroupBuilder MapProcessBuyCard(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/buy-card",
				async (Guid gameId, ProcessBuyCardRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					if (request.PlayerId == Guid.Empty)
					{
						return Results.BadRequest(new { Message = "PlayerId is required." });
					}

					var command = new ProcessBuyCardCommand(gameId, request.PlayerId, request.Accept);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ProcessBuyCardErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ProcessBuyCardErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							ProcessBuyCardErrorCode.NoPendingOffer => Results.Conflict(new { error.Message }),
							ProcessBuyCardErrorCode.InsufficientChips => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"Baseball{nameof(MapProcessBuyCard).TrimPrefix("Map")}")
			.WithSummary("Process Buy Card")
			.WithDescription("Processes a buy-card decision for Baseball poker when a 4 is dealt face-up.")
			.Produces<ProcessBuyCardSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}

public record ProcessBuyCardRequest
{
	public Guid PlayerId { get; init; }
	public bool Accept { get; init; }
}
