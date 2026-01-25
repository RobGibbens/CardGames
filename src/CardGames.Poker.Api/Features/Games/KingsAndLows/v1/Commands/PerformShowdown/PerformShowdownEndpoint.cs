using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Services;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;

/// <summary>
/// Endpoint for performing the showdown in a Kings and Lows game.
/// </summary>
public static class PerformShowdownEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for performing showdown.
	/// </summary>
	public static RouteGroupBuilder MapPerformShowdown(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/showdown",
				async (Guid gameId, IMediator mediator, IGameStateBroadcaster broadcaster, CancellationToken cancellationToken) =>
				{
					var command = new PerformShowdownCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return await result.Match(
						async success =>
						{
							// Broadcast updated game state to all players after showdown
							await broadcaster.BroadcastGameStateAsync(gameId, cancellationToken);
							return Results.Ok(success);
						},
						error => Task.FromResult(error.Code switch
						{
							PerformShowdownErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							PerformShowdownErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						})
					);
				})
			.WithName($"KingsAndLows{nameof(MapPerformShowdown).TrimPrefix("Map")}")
			.WithSummary("Perform Showdown")
			.WithDescription(
				"Performs the showdown phase to evaluate all remaining players' hands and award the pot to the winner(s). " +
				"Kings and Lows uses wild card evaluation: Kings are always wild, and the lowest non-King card(s) are also wild.\n\n" +
				"**Wild Card Rules:**\n" +
				"- All Kings are wild\n" +
				"- The lowest-ranked card(s) in each player's hand (excluding Kings) are also wild\n" +
				"- Wild cards can represent any card to form the best possible hand\n\n" +
				"**Showdown Scenarios:**\n" +
				"- **Win by fold:** If only one player stays, they win the pot without showing cards\n" +
				"- **Single winner:** The player with the highest-ranking hand wins the pot\n" +
				"- **Split pot:** If multiple players tie, the pot is divided equally among winners\n\n" +
				"**Response includes:**\n" +
				"- Payouts to each winning player\n" +
				"- Evaluated hand information with wild card indexes\n" +
				"- Winners and losers lists (losers must match the pot)")
			.Produces<PerformShowdownSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
