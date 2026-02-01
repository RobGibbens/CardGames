using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Endpoint for starting a new hand in any poker game.
/// </summary>
public static class StartHandEndpoint
{
    /// <summary>
    /// Maps the POST endpoint for starting a new hand.
    /// </summary>
    /// <param name="group">The route group builder.</param>
    /// <returns>The route group builder for chaining.</returns>
    public static RouteGroupBuilder MapStartHand(this RouteGroupBuilder group)
    {
        group.MapPost("{gameId:guid}/hands",
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
                            StartHandErrorCode.UnsupportedGameType => Results.BadRequest(new { error.Message }),
                            _ => Results.Problem(error.Message)
                        }
                    );
                })
            .WithName($"Generic{nameof(MapStartHand).TrimPrefix("Map")}")
            .WithSummary("Start Hand (Generic)")
            .WithDescription("Starts a new hand in any poker game variant. The game type is automatically detected " +
                             "from the game entity and routed to the appropriate flow handler for game-specific initialization. " +
                             "After calling this endpoint, the game transitions to the initial phase determined by the game type " +
                             "(e.g., CollectingAntes for most games, Dealing for Kings and Lows).")
            .Produces<StartHandSuccessful>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
