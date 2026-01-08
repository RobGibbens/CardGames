using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.JoinGame;

/// <summary>
/// Endpoint for joining a game at a specific seat.
/// </summary>
public static class JoinGameEndpoint
{
    /// <summary>
    /// Maps the POST endpoint for joining a game.
    /// </summary>
    public static RouteGroupBuilder MapJoinGame(this RouteGroupBuilder group)
    {
        group.MapPost("{gameId:guid}/players",
                async (Guid gameId, JoinGameRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var command = new JoinGameCommand(gameId, request.SeatIndex, request.StartingChips);
                    var result = await mediator.Send(command, cancellationToken);

                    return result.Match(
                        success => Results.Ok(success),
                        error => error.Code switch
                        {
                            JoinGameErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
                            JoinGameErrorCode.SeatOccupied => Results.Conflict(new { error.Message }),
                            JoinGameErrorCode.AlreadySeated => Results.Conflict(new { error.Message }),
                            JoinGameErrorCode.MaxPlayersReached => Results.Conflict(new { error.Message }),
                            JoinGameErrorCode.InvalidSeatIndex => Results.BadRequest(new { error.Message }),
                            JoinGameErrorCode.GameEnded => Results.Conflict(new { error.Message }),
                            _ => Results.Problem(error.Message)
                        }
                    );
                })
            .WithName($"SevenCardStud{nameof(MapJoinGame).TrimPrefix("Map")}")
            .WithSummary("Join Game")
            .WithDescription(
                "Adds the currently authenticated player to a game at the specified seat. " +
                "Players can join a game at any time as long as the game hasn't ended and " +
                "there are seats available. If joining mid-hand, the player will sit out " +
                "the current hand and join play on the next hand.\n\n" +
                "**Validations:**\n" +
                "- Game must exist and not be ended\n" +
                "- Seat must be available (not already occupied)\n" +
                "- Player must not already be seated elsewhere in the game\n" +
                "- Game must not have reached maximum player count\n\n" +
                "**Response:**\n" +
                "- `CanPlayCurrentHand`: true if joining during WaitingToStart/WaitingForPlayers phase")
            .Produces<JoinGameSuccessful>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
