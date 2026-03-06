using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;

/// <summary>
/// Endpoint for starting a new hand in a Texas Hold 'Em game.
/// </summary>
public static class StartHandEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for starting a new hand.
	/// </summary>
	public static RouteGroupBuilder MapStartHand(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/start",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new StartHandCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							StartHandErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							StartHandErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							StartHandErrorCode.NotEnoughPlayers => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"HoldEm{nameof(MapStartHand).TrimPrefix("Map")}")
			.WithSummary("Start Hand")
			.WithDescription("Starts a new Hold'Em hand, posts blinds, and deals hole cards.")
			.Produces<StartHandSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
