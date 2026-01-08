using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.PerformShowdown;

/// <summary>
/// Endpoint for performing the showdown in a Seven Card Stud game.
/// </summary>
public static class PerformShowdownEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for performing showdown.
	/// </summary>
	public static RouteGroupBuilder MapPerformShowdown(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/showdown",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new PerformShowdownCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							PerformShowdownErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							PerformShowdownErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"SevenCardStud{nameof(MapPerformShowdown).TrimPrefix("Map")}")
			.WithSummary("Perform Showdown")
			.WithDescription(
				"Performs the showdown phase to evaluate all remaining players' hands and award the pot(s) to the winner(s). " +
				"After the showdown completes, the game transitions to the Complete phase and the dealer button moves to the next position.\n\n" +
				"**Showdown Scenarios:**\n" +
				"- **Win by fold:** If only one player remains (all others folded), they win the entire pot without showing cards\n" +
				"- **Single winner:** The player with the highest-ranking hand wins the entire pot\n" +
				"- **Split pot:** If multiple players tie with the same hand strength, the pot is divided equally among winners\n" +
				"- **Side pots:** When players are all-in for different amounts, side pots are calculated and awarded separately\n\n" +
				"**Response includes:**\n" +
				"- Payouts to each winning player\n" +
				"- Evaluated hand information for all participating players\n" +
				"- Whether the hand was won by fold (no showdown required)")
			.Produces<PerformShowdownSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}

