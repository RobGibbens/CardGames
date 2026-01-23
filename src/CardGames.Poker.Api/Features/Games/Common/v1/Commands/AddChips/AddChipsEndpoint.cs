using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Endpoint for adding chips to a player's stack.
/// </summary>
public static class AddChipsEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for adding chips.
	/// </summary>
	public static RouteGroupBuilder MapAddChips(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/players/{playerId:guid}/add-chips",
			async (Guid gameId, Guid playerId, AddChipsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
			{
				var command = new AddChipsCommand(gameId, playerId, request.Amount);
				var result = await mediator.Send(command, cancellationToken);

				return result.Match(
					success => Results.Ok(success),
					error => error.Code switch
					{
						AddChipsErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
						AddChipsErrorCode.PlayerNotInGame => Results.BadRequest(new { error.Message }),
						AddChipsErrorCode.InvalidAmount => Results.BadRequest(new { error.Message }),
						AddChipsErrorCode.GameEnded => Results.BadRequest(new { error.Message }),
						_ => Results.Problem(error.Message)
					}
				);
			})
			.WithName($"{nameof(MapAddChips).TrimPrefix("Map")}")
			.WithSummary("Add Chips")
			.WithDescription(
				"Adds chips to a player's stack in the game. " +
				"For Kings and Lows, chips are added immediately. " +
				"For other game types, chips are added immediately if the game is between hands, " +
				"otherwise they are queued and will be added at the start of the next hand.\n\n" +
				"**Validations:**\n" +
				"- Game must exist and not be ended\n" +
				"- Player must be part of the game\n" +
				"- Amount must be positive\n\n" +
				"**Response:**\n" +
				"- `AppliedImmediately`: true if chips were added to stack, false if queued\n" +
				"- `PendingChipsToAdd`: total pending chips waiting to be applied\n" +
				"- `NewChipStack`: current chip stack (may not include pending chips)")
			.Produces<AddChipsResponse>()
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound);

		return group;
	}
}
