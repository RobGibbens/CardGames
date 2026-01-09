using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;

/// <summary>
/// Endpoint for deleting a game.
/// </summary>
public static class DeleteGameEndpoint
{
	/// <summary>
	/// Maps the DELETE endpoint for deleting a game.
	/// </summary>
	public static RouteGroupBuilder MapDeleteGame(this RouteGroupBuilder group)
	{
		group.MapDelete("{gameId:guid}",
				async Task<IResult> (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new DeleteGameCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							DeleteGameErrorCode.GameNotFound =>
								Results.NotFound(new { error.Message }),
							DeleteGameErrorCode.NotAuthorized =>
								Results.Problem(
									title: "Not Authorized",
									detail: error.Message,
									statusCode: StatusCodes.Status403Forbidden),
							DeleteGameErrorCode.AlreadyDeleted =>
								Results.Problem(
									title: "Already Deleted",
									detail: error.Message,
									statusCode: StatusCodes.Status410Gone),
							_ => Results.Problem(
									title: "Delete Failed",
									detail: error.Message,
									statusCode: StatusCodes.Status500InternalServerError)
						}
					);
				})
			.WithName($"{nameof(MapDeleteGame).TrimPrefix("Map")}")
			.WithSummary("Delete Game")
			.WithDescription(
				"Soft deletes a game. " +
				"Can only be called by the user who created the game.\n\n" +
				"**Validations:**\n" +
				"- Game must exist\n" +
				"- Caller must be the game creator\n" +
				"- Game must not already be deleted")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status410Gone);

		return group;
	}
}

