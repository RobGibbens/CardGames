using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;

public static class AceChoiceEndpoint
{
	public static RouteGroupBuilder MapAceChoice(this RouteGroupBuilder group)
	{
		group.MapPost("/{gameId:guid}/ace-choice",
				async (Guid gameId, AceChoiceRequest request, IMediator mediator, IGamePlayerResolver players,
					CancellationToken cancellationToken) =>
				{
					var seated = await players.ResolveSeatedPlayerAsync(gameId, cancellationToken);
					if (seated is null) return SeatedActor.NotSeated();

					var command = new AceChoiceCommand(gameId, seated.PlayerId, request.AceIsHigh);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						Results.Ok,
						error => Results.Problem(
							title: "Ace choice error",
							detail: error.Message,
							statusCode: error.Code == AceChoiceErrorCode.GameNotFound
								? StatusCodes.Status404NotFound
								: StatusCodes.Status400BadRequest)
					);
				})
			.WithName($"InBetween{nameof(MapAceChoice).TrimPrefix("Map")}")
			.WithSummary(nameof(MapAceChoice).TrimPrefix("Map"))
			.WithDescription("Declare an Ace as high or low when it is the first boundary card in In-Between.")
			.Produces<AceChoiceSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest);

		return group;
	}
}

/// <summary>
///     Request body for ace high/low choice.
/// </summary>
public record AceChoiceRequest(bool AceIsHigh);