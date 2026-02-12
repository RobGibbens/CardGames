using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;

/// <summary>
/// Endpoint for dealing hands to all players in a Follow the Queen game.
/// </summary>
public static class DealHandsEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for dealing hands.
	/// </summary>
	public static RouteGroupBuilder MapDealHands(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/hands/deal",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new DealHandsCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							DealHandsErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							DealHandsErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							DealHandsErrorCode.InsufficientCards => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"FollowTheQueen{nameof(MapDealHands).TrimPrefix("Map")}")
			.WithSummary("Deal Hands")
			.WithDescription("Deals cards for the current street in Follow the Queen. " +
			                 "Third Street: deals 2 hole cards (face-down) + 1 board card (face-up) to each player. " +
			                 "Fourth-Sixth Street: deals 1 board card (face-up) to each player. " +
			                 "Seventh Street: deals 1 hole card (face-down) to each player. " +
			                 "After dealing, the game transitions to the appropriate betting phase.")
			.Produces<DealHandsSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}
