using MediatR;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;

/// <summary>
/// Endpoint for folding during the Irish Hold 'Em discard phase (e.g., when the timer expires).
/// </summary>
public static class FoldDuringDrawEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for folding during the discard phase.
	/// </summary>
	public static RouteGroupBuilder MapFoldDuringDraw(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/fold-during-draw",
				async (Guid gameId, FoldDuringDrawRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new FoldDuringDrawCommand(gameId, request.PlayerSeatIndex);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => Results.Conflict(new { error.Message })
					);
				})
			.WithName("IrishHoldEmFoldDuringDraw")
			.WithSummary("Fold During Draw Phase")
			.WithDescription(
				"Folds a player during the Irish Hold 'Em discard phase, typically when the turn timer " +
				"expires without the player selecting their 2 discards. This is used because standing pat " +
				"(keeping all 4 cards) is not valid in Irish Hold 'Em.\n\n" +
				"**Phase Transitions:**\n" +
				"- If only one player remains: transitions to Showdown\n" +
				"- If other players still need to discard: advances to next draw player\n" +
				"- If all remaining players have discarded: transitions to DrawComplete → Turn");

		return group;
	}
}

/// <summary>
/// Request body for folding during the discard phase.
/// </summary>
/// <param name="PlayerSeatIndex">The seat index of the player to fold.</param>
public record FoldDuringDrawRequest(int PlayerSeatIndex);
