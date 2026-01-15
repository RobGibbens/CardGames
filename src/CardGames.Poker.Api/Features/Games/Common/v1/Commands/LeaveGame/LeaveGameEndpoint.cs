using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Endpoint for leaving a game table.
/// </summary>
public static class LeaveGameEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for leaving a game.
	/// </summary>
	public static RouteGroupBuilder MapLeaveGame(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/leave",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new LeaveGameCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.NotFound(new { error.Message }));
				})
			.WithName($"{nameof(MapLeaveGame).TrimPrefix("Map")}")
			.WithSummary("Leave Game")
			.WithDescription(
				"Removes the currently authenticated player from a game table. " +
				"Players can leave at any time:\n\n" +
				"**Pre-Game (Not Started):**\n" +
				"- Player record is completely deleted\n" +
				"- No history is retained\n" +
				"- Seat becomes immediately available\n\n" +
				"**Mid-Game (Between Hands):**\n" +
				"- Player is marked as Left and removed from active play\n" +
				"- Participation record is preserved with final chip count\n" +
				"- Seat appears empty immediately\n\n" +
				"**Mid-Game (During Active Hand):**\n" +
				"- Player must finish the current hand\n" +
				"- Leave is queued and executed after hand completes\n" +
				"- Player receives a message indicating they will leave after the hand\n\n" +
				"**Response:**\n" +
				"- `Immediate`: true if player left immediately, false if queued for end of hand")
			.Produces<LeaveGameSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound);

		return group;
	}
}
