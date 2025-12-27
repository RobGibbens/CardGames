using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.UpdateTableSettings;

/// <summary>
/// Endpoint for updating table settings.
/// </summary>
public static class UpdateTableSettingsEndpoint
{
    /// <summary>
    /// Maps the PUT endpoint for updating table settings.
    /// </summary>
    public static RouteGroupBuilder MapUpdateTableSettings(this RouteGroupBuilder group)
    {
        group.MapPut("{gameId:guid}/settings",
                async (Guid gameId, UpdateTableSettingsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateTableSettingsCommand(
                        gameId,
                        request.Name,
                        request.Ante,
                        request.MinBet,
                        request.SmallBlind,
                        request.BigBlind,
                        request.RowVersion);

                    var result = await mediator.Send(command, cancellationToken);

                    return result.Match(
                        success => Results.Ok(success.Settings),
                        error => error.Code switch
                        {
                            UpdateTableSettingsErrorCode.GameNotFound =>
                                Results.NotFound(new { error.Message }),
                            UpdateTableSettingsErrorCode.NotAuthorized =>
                                Results.Problem(
                                    title: "Not Authorized",
                                    detail: error.Message,
                                    statusCode: StatusCodes.Status403Forbidden),
                            UpdateTableSettingsErrorCode.PhaseNotEditable =>
                                Results.Problem(
                                    title: "Cannot Edit",
                                    detail: error.Message,
                                    statusCode: StatusCodes.Status409Conflict),
                            UpdateTableSettingsErrorCode.ConcurrencyConflict =>
                                Results.Problem(
                                    title: "Concurrency Conflict",
                                    detail: error.Message,
                                    statusCode: StatusCodes.Status409Conflict),
                            UpdateTableSettingsErrorCode.ValidationFailed =>
                                Results.BadRequest(new { error.Message }),
                            _ => Results.Problem(error.Message)
                        }
                    );
                })
            .WithName(nameof(MapUpdateTableSettings).TrimPrefix("Map"))
            .WithSummary("Update Table Settings")
            .WithDescription(
                "Updates the table settings for a game. " +
                "Can only be called when the game is in WaitingToStart or WaitingForPlayers phase. " +
                "Requires the caller to be the table creator.\n\n" +
                "**Validations:**\n" +
                "- Game must exist\n" +
                "- Caller must be the table creator\n" +
                "- Game must be in an editable phase\n" +
                "- RowVersion must match current value (optimistic concurrency)\n" +
                "- BigBlind >= SmallBlind if both provided\n" +
                "- Ante >= 0\n" +
                "- MinBet > 0")
            .Produces<UpdateTableSettingsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}
