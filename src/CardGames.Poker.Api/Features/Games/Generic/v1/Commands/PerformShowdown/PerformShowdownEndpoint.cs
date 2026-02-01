using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;

/// <summary>
/// Endpoint for performing the showdown in any poker game.
/// </summary>
public static class PerformShowdownEndpoint
{
    /// <summary>
    /// Maps the POST endpoint for performing showdown.
    /// </summary>
    /// <param name="group">The route group builder.</param>
    /// <returns>The route group builder for chaining.</returns>
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
                            PerformShowdownErrorCode.NoValidHands => Results.BadRequest(new { error.Message }),
                            PerformShowdownErrorCode.UnsupportedGameType => Results.BadRequest(new { error.Message }),
                            _ => Results.Problem(error.Message)
                        }
                    );
                })
            .WithName($"Generic{nameof(MapPerformShowdown).TrimPrefix("Map")}")
            .WithSummary("Perform Showdown (Generic)")
            .WithDescription(
                "Performs the showdown phase in any poker game variant to evaluate all remaining players' hands and award the pot(s) to the winner(s). " +
                "The game type is automatically detected and the appropriate hand evaluator is used for evaluation.\n\n" +
                "**Game-Specific Behavior:**\n" +
                "- **Five Card Draw:** Standard 5-card poker hand evaluation\n" +
                "- **Seven Card Stud:** Best 5 cards from 7-card hand\n" +
                "- **Kings and Lows:** Wild card evaluation (Kings + lowest cards are wild), transitions to PotMatching phase\n\n" +
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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
