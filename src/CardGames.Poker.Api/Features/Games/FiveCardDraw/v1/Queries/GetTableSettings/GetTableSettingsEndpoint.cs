using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetTableSettings;

/// <summary>
/// Endpoint for retrieving table settings.
/// </summary>
public static class GetTableSettingsEndpoint
{
    /// <summary>
    /// Maps the GET endpoint for retrieving table settings.
    /// </summary>
    public static RouteGroupBuilder MapGetTableSettings(this RouteGroupBuilder group)
    {
        group.MapGet("{gameId:guid}/settings",
                async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var query = new GetTableSettingsQuery(gameId);
                    var result = await mediator.Send(query, cancellationToken);

                    return result is null
                        ? Results.NotFound(new { Message = $"Game with ID {gameId} not found." })
                        : Results.Ok(result);
                })
            .WithName(nameof(MapGetTableSettings).TrimPrefix("Map"))
            .WithSummary("Get Table Settings")
            .WithDescription(
                "Retrieves the current table settings for a game. " +
                "Returns configuration details including ante, blinds, and current phase. " +
                "The `IsEditable` property indicates whether settings can currently be modified.")
            .Produces<GetTableSettingsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
