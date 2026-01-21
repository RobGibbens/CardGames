using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;

/// <summary>
/// Endpoint for retrieving hand history with per-player results for expandable display.
/// </summary>
public static class GetHandHistoryWithPlayersEndpoint
{
    /// <summary>
    /// Maps the GetHandHistoryWithPlayers endpoint.
    /// </summary>
    public static RouteGroupBuilder MapGetHandHistoryWithPlayers(this RouteGroupBuilder group)
    {
        group.MapGet("{gameId:guid}/history/detailed",
                async (Guid gameId, IMediator mediator, int take = 25, int skip = 0, CancellationToken cancellationToken = default) =>
                {
                    var query = new GetHandHistoryWithPlayersQuery(gameId, take, skip);
                    var response = await mediator.Send(query, cancellationToken);
                    return Results.Ok(response);
                })
            .WithName($"{nameof(MapGetHandHistoryWithPlayers).TrimPrefix("Map")}")
            .WithSummary("Get detailed hand history with per-player results")
            .WithDescription("Retrieves the hand history for a specific game with per-player results for each hand. " +
                           "Includes player names, final actions (Won/Lost/SplitPot/Folded), net amounts, and visible cards at showdown. " +
                           "Ordered newest-first. Use this endpoint for expandable hand history display.")
            .MapToApiVersion(1.0)
            .Produces<HandHistoryWithPlayersListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
