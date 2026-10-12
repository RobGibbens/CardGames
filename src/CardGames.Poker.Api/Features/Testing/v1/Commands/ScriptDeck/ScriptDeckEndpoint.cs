using MediatR;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.ScriptDeck;

public static class ScriptDeckEndpoint
{
	public static RouteGroupBuilder MapScriptDeck(this RouteGroupBuilder group)
	{
		group.MapPost("games/{gameId:guid}/deck",
				async (Guid gameId, ScriptDeckRequest request, IMediator mediator,
					CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new ScriptDeckCommand(gameId, request.Cards), cancellationToken);

					return result.Match(
						_ => Results.NoContent(),
						error => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest));
				})
			.WithName("ScriptDeck")
			.WithSummary("Script the next deal")
			.WithDescription(
				"Stores a 52-card order for the next initial deal of the game. The following hand is shuffled normally.")
			.Produces(StatusCodes.Status204NoContent)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.AllowAnonymous();

		return group;
	}
}

public sealed class ScriptDeckRequest
{
	public IReadOnlyList<string> Cards { get; init; } = [];
}
