using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Endpoint for toggling table odds visibility.
/// </summary>
public static class ToggleOddsVisibilityEndpoint
{
    /// <summary>
    /// Maps the PUT endpoint for toggling odds visibility.
    /// </summary>
    public static RouteGroupBuilder MapToggleOddsVisibility(this RouteGroupBuilder group)
    {
        group.MapPut("{gameId:guid}/settings/odds-visibility",
                async (Guid gameId, ToggleOddsVisibilityRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var command = new ToggleOddsVisibilityCommand(gameId, request.AreOddsVisibleToAllPlayers);
                    var result = await mediator.Send(command, cancellationToken);

                    return result.Match(
                        success => Results.Ok(success),
                        error => error.Code switch
                        {
                            ToggleOddsVisibilityErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
                            ToggleOddsVisibilityErrorCode.NotAuthorized =>
                                Results.Problem(
                                    title: "Not Authorized",
                                    detail: error.Message,
                                    statusCode: StatusCodes.Status403Forbidden),
                            ToggleOddsVisibilityErrorCode.GameEnded =>
                                Results.Problem(
                                    title: "Game Ended",
                                    detail: error.Message,
                                    statusCode: StatusCodes.Status409Conflict),
                            _ => Results.Problem(error.Message)
                        }
                    );
                })
            .WithName($"{nameof(MapToggleOddsVisibility).TrimPrefix("Map")}")
            .WithSummary("Toggle Odds Visibility")
            .WithDescription(
                "Allows the table host to toggle whether hand-odds are visible to all players. " +
                "This can be changed while a game is active.\n\n" +
                "**Validations:**\n" +
                "- Game must exist\n" +
                "- Caller must be the table creator\n" +
                "- Game must not be ended")
            .Produces<ToggleOddsVisibilitySuccessful>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
